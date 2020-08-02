﻿using System;
using System.Globalization;
using System.Text;
using System.IO;

namespace ChangeEncoding
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string dir = @"R:\Trunk";
                Console.WriteLine($"Checking \"{dir}\"...");
                var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);

                string progressFormat = "{0," + files.Length.ToString().Length + "}";
                string progressTotal = " of " + files.Length.ToString() + ".";
                int i = 0;
                string filePath;
                Encoding encoding;
                var targetEncoding = Encoding.UTF8;
                var defaultEncoding = Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                while (i < files.Length)
                {
                    filePath = files[i];
                    Console.CursorLeft = 0;
                    Console.Write("File " + String.Format(progressFormat, ++i) + progressTotal);
                    Console.WriteLine(": " + files[i]);
                    using (var file = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        encoding = GetEncodingFromBOM(file);
                        if (encoding == targetEncoding)
                            continue;

                        string text;
                        using (var reader = new StreamReader(file, encoding ?? defaultEncoding))
                        using (var writer = new StreamWriter(file, targetEncoding))
                        {
                            text = reader.ReadToEnd();
                            // cant dispose of reader, because it will close the stream.
                            file.Position = 0;
                            writer.Write(text);
                            // https://stackoverflow.com/questions/8464261/filestream-and-streamwriter-how-to-truncate-the-remainder-of-the-file-after-wr
                            writer.Flush();
                            file.SetLength(file.Position);
                        }
                    }
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
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
    }
}
