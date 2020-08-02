using System;
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
                while (i < files.Length)
                {
                    filePath = files[i];
                    Console.CursorLeft = 0;
                    Console.Write("File " + String.Format(progressFormat, ++i) + progressTotal);
                    Console.WriteLine(": " + files[i]);
                    using (var file = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    using (var textReader = new StreamReader(,))
                    {

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
                    if (bytesRead > 2 && buffer[1] == 0xbb && buffer[2] == 0xbf)
                        return Encoding.UTF8;
                    break;
                case 0xFE:
                    if (bytesRead > 1 && buffer[1] == 0xff)
                        return Encoding.Unicode;
                    break;
                case 0x00:
                    if (bytesRead > 3 && buffer[1] == 0x00 && buffer[2] == 0xfe && buffer[3] == 0xff)
                        return Encoding.UTF32;
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
