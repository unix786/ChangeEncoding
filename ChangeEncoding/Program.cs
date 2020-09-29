using System;
using System.IO;
using System.Text;

namespace ChangeEncoding
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string dir = @"E:\SupportedSolutions\";
                Console.WriteLine($"Checking \"{dir}\"...");
                var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
                bool addBom = false;
                bool convertNontargetFiles = true;
                bool checkAndFixExistingUtfFiles = true;

                string progressFormat = "{0," + files.Length.ToString().Length + "}";
                string progressTotal = " of " + files.Length.ToString() + ".";
                int i = 0;
                string filePath;
                Encoding encoding;

                var targetEncoding = Encoding.UTF8;
                var defaultEncoding = Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                if (!addBom && targetEncoding != Encoding.UTF8) throw new NotSupportedException("Cannot do non-BOM for chosen encoding.");

                int t = 0;
                int tc = 0;
                int errors = 0;
                bool hasUnicodeChars;
                while (i < files.Length)
                {
                    filePath = files[i];
                    ClearLine();
                    Console.Write("File " + String.Format(progressFormat, ++i) + progressTotal);
                    Console.Write(": " + filePath);
                    try
                    {
                        using (var file = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                        {
                            encoding = GetEncodingFromBOM(file);
                            if (encoding == targetEncoding)
                            {
                                t++;
                                if (checkAndFixExistingUtfFiles && encoding == Encoding.UTF8) CheckAndFixUtfFile(file, defaultEncoding);
                                continue;
                            }
                            else if (targetEncoding == Encoding.UTF8 && IsUTF8Compliant(file, out hasUnicodeChars))
                            {
                                t++;
                                if (hasUnicodeChars)
                                {
                                    tc++;
                                    Console.WriteLine(". UTF8 compliant.");
                                    if (addBom) Convert(file, targetEncoding, targetEncoding);
                                    continue;
                                }
                                else
                                {
                                    // Probably an ASCII file without higher order characters.
                                }
                            }
                            Console.WriteLine(". Converting...");
                            if (convertNontargetFiles) Convert(file, encoding ?? defaultEncoding, addBom ? targetEncoding : null);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Console.WriteLine();
                        Console.WriteLine("! " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
                Console.WriteLine();
                Console.WriteLine((t == files.Length ? "All" : t.ToString()) + " of " + files.Length + " files had target encoding.");
                if (tc > 0) Console.WriteLine(tc.ToString() + " of the files had UTF8 characters without a BOM.");
                if (errors > 0) Console.WriteLine("There were " + errors.ToString() + " errors.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }

        private static void ClearLine()
        {
            //int cursor = Console.CursorLeft;
            //Console.CursorLeft = 0;
            //Console.Write(new string(' ', cursor));
            //Console.CursorLeft = 0;
            if (Console.CursorLeft > 0) Console.WriteLine();
        }

        private static void Convert(FileStream file, Encoding encoding, Encoding targetEncoding)
        {
            string text;
            using (var reader = new StreamReader(file, encoding))
            using (var writer = targetEncoding == null ? new StreamWriter(file) : new StreamWriter(file, targetEncoding))
            {
                text = reader.ReadToEnd();
                // cant dispose of reader, because it will close the stream.
                file.Position = 0;
                writer.Write(text);
                EndWriteAndTrim(file, writer);
            }
        }

        private static void EndWriteAndTrim(FileStream file, StreamWriter writer)
        {
            // https://stackoverflow.com/questions/8464261/filestream-and-streamwriter-how-to-truncate-the-remainder-of-the-file-after-wr
            writer.Flush();
            file.SetLength(file.Position);
        }

        /// <summary>
        /// Detects the byte order mark of a file and returns an appropriate encoding for the file or <see langword="null"/>.
        /// <see cref="Stream.Position"/> gets restored to previous position after reading.
        /// </summary>
        /// <remarks>
        /// Method based on: https://weblog.west-wind.com/posts/2007/nov/28/detecting-text-encoding-for-streamreader
        /// <para/>Byte order marks: http://unicode.org/faq/utf_bom.html#BOM
        /// </remarks>
        public static Encoding GetEncodingFromBOM(Stream fileStream)
        {
            byte[] buffer = new byte[5];
            int bytesRead = fileStream.Read(buffer, 0, 4);
            fileStream.Seek(-bytesRead, SeekOrigin.Current);

            if (bytesRead < 2)
                return null;

            switch (buffer[0])
            {
                case 0xEF:
                    if (bytesRead > 2 && buffer[1] == 0xBB && buffer[2] == 0xBF)
                        return Encoding.UTF8;
                    break;
                case 0xFF:
                    if (bytesRead > 1 && buffer[1] == 0xFE)
                    {
                        if (bytesRead > 3 && buffer[2] == 0x00 && buffer[3] == 0x00)
                            return Encoding.UTF32;
                        return Encoding.Unicode;
                    }
                    break;
                case 0xFE:
                    if (bytesRead > 1 && buffer[1] == 0xFF)
                        return Encoding.BigEndianUnicode;
                    break;
                case 0x00:
                    if (bytesRead > 3 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                        throw new NotSupportedException("UTF-32 big-endian");
                    break;
                case 0x2B:
                    if (bytesRead > 2 && buffer[1] == 0x2f && buffer[2] == 0x76)
                        return Encoding.UTF7;
                    break;
            }
            return null;
        }

        /// <summary>
        /// Fixes UTF8 files that have ASCII (<paramref name="defaultEncoding"/>) mixed into them.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="defaultEncoding">Encoding of the non-UTF parts.</param>
        private static void CheckAndFixUtfFile(FileStream file, Encoding defaultEncoding)
        {
            if (IsUTF8Compliant(file, out _, stopAtFault: true))
                return;

            Console.Write(". Possible mixed encoding. Attempting to fix.");
            int remainingBytes = (int)(file.Length - file.Position);
            using (var reader = new StreamReader(file, defaultEncoding))
            using (var memory = new MemoryStream(2 * remainingBytes))
            using (var memoryWriter = new StreamWriter(memory))
            {
                long firstFaultPosition = file.Position;
                var faultPosition = firstFaultPosition;
                long nextFaultPosition, faultEndPosition;
                byte b;
                bool skipHighOrderChars = true;
                do
                {
                    do
                    {
                        b = (byte)file.ReadByte();
                        if ((b & (1 << 7)) == 0)
                        {
                            skipHighOrderChars = false;
                            continue;
                        }

                        if (skipHighOrderChars)
                            continue;

                        faultEndPosition = file.Seek(-1, SeekOrigin.Current);
                        if (IsUTF8Compliant(file, out _, stopAtFault: true))
                        {
                            ConvertChunk(file, faultPosition, faultEndPosition, reader, memoryWriter);
                            memoryWriter.Flush();
                            // The rest of the file is UTF8. Doing raw copy of bytes.
                            file.CopyTo(memory);

                            WriteFromMemory(file, memory, firstFaultPosition);
                            // No need to trim the file, because the size likely increased.
                            return;
                        }
                        else
                        {
                            if (faultEndPosition < file.Position)
                            {
                                // Convert chunk and copy some more bytes without converting.
                                nextFaultPosition = file.Position;
                                ConvertChunk(file, faultPosition, faultEndPosition, reader, memoryWriter);
                                // Flushing, because the next write will bypass this writer.
                                memoryWriter.Flush();
                                var buffer = new byte[nextFaultPosition - file.Position];
                                file.Read(buffer, 0, buffer.Length);
                                memory.Write(buffer, 0, buffer.Length);
                                faultPosition = file.Position;
                            }
                            skipHighOrderChars = true;
                        }
                    } while (file.Position < file.Length);
                } while (file.Position < file.Length);

                // The rest of the file needs to be converted.
                file.Position = faultPosition;
                reader.DiscardBufferedData();
                var text = reader.ReadToEnd();
                if(memory.Position > 0)
                {
                    // Some of the file has already been converted.
                    WriteFromMemory(file, memory, firstFaultPosition);
                }
                else
                {
                    // Here: faultPosition == firstFaultPosition
                    file.Position = faultPosition;
                }
                memoryWriter.Write(text);
                // No need to trim the file, because the size likely increased.
            }
        }

        private static void WriteFromMemory(FileStream file, MemoryStream memory, long fileStartPosition)
        {
            memory.Position = 0;
            file.Position = fileStartPosition;
            memory.CopyTo(file);
        }

        private static void ConvertChunk(FileStream file, long faultPosition, long faultEndPosition, StreamReader reader, StreamWriter writer)
        {
            var buffer = new char[faultEndPosition - faultPosition];
            file.Position = faultPosition;
            reader.DiscardBufferedData();
            reader.Read(buffer, 0, buffer.Length);
            // StreamReader tends to read ahead, so need to reposition the stream.
            file.Position = faultEndPosition;
            reader.DiscardBufferedData();
            writer.Write(buffer);
        }

        public static bool IsUTF8Compliant(Stream stream, out bool hasUnicodeChars, bool stopAtFault = false)
        {
            long initialPosition = stream.Position;
            try
            {
                byte b;
                hasUnicodeChars = false;
                int expectedBytes = 0;
                while (stream.Position < stream.Length)
                {
                    b = (byte)stream.ReadByte();
                    if ((b & (1 << 7)) == 0)
                    {
                        if (expectedBytes > 0)
                            return false;
                    }
                    else
                    {
                        if (expectedBytes > 0)
                        {
                            if ((b & (1 << 6)) != 0)
                                return false;
                            expectedBytes -= 1;
                        }
                        else
                        {
                            if (stopAtFault) initialPosition = stream.Position - 1;
                            int p;
                            for (p = 6; p >= 0 && (b & (1 << p)) != 0; p--) ;
                            expectedBytes = 6 - p;
                            if (expectedBytes == 0 || expectedBytes > 3)
                                return false;
                            hasUnicodeChars = true;
                        }
                    }
                }
                if (expectedBytes == 0)
                    return true;

                hasUnicodeChars = false;
                return false;
            }
            finally
            {
                stream.Position = initialPosition;
            }
        }
    }
}
