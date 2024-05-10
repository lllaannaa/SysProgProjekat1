using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebServer
{
    class Program
    {
        static Dictionary<string, string[]> cache = new Dictionary<string, string[]>();
        static readonly object cacheLock = new object();
        static string rootDirectory = @"C:\"; // Putanja do root direktorijuma

        static void Main(string[] args)
        {
            // Definisanje TCP listenera
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 5050);
            listener.Start();
            Console.WriteLine("Server started...");

            // Beskonacna petlja koja ceka zahteve
            while (true)
            {
                // Prihvatanje klijentskog zahteva
                TcpClient client = listener.AcceptTcpClient();

                // Kreiranje novog zadatka (thread) za svaki klijentski zahtev
                ThreadPool.QueueUserWorkItem(ProcessRequest, client);
            }
        }

        static void ProcessRequest(object stateInfo)
        {
            // Dobavljanje klijentskog TCP clienta
            TcpClient client = stateInfo as TcpClient;

            // Dobavljanje klijentskog zahteva
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            string request = reader.ReadLine();
            Console.WriteLine($"Request: {request}");

            // Parsiranje parametara iz GET zahteva
            string keyword = "";
            string[] requestParts = request.Split(' ');
            if (requestParts.Length >= 2)
            {
                string url = requestParts[1];
                int lastSlashIndex = url.LastIndexOf('/');
                if (lastSlashIndex != -1)
                {
                    keyword = url.Substring(lastSlashIndex + 1);
                }
            }
           // Console.WriteLine($"Keyword: {keyword}"); //provera kljucne reci

            // Provera kesa
            string[] cachedFiles;
            lock (cacheLock)
            {
                if (cache.TryGetValue(keyword, out cachedFiles))
                {
                    Console.WriteLine($"Files retrieved from cache for keyword: {keyword}"); //Provera da li je fajl pribavljen iz kesa
                    SendResponse(stream, cachedFiles);
                    return;
                }
            }

            // Paralelno pretrazivanje fajlova samo u root direktorijumu i filtriranje po kljucnoj reci
            var files = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(file => Path.GetFileName(file).Contains(keyword))
                .ToArray();

            lock (cacheLock)
            {
                cache[keyword] = files;
            }

            // Slanje odgovora klijentu
            SendResponse(stream, files);

            // Zatvaranje resursa
            reader.Close();
            stream.Close();
            client.Close();
        }

        static void SendResponse(NetworkStream stream, string[] files)
        {
            // Kreiranje odgovora
            StringBuilder responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("HTTP/1.1 200 OK");
            responseBuilder.AppendLine("Content-Type: text/html");
            responseBuilder.AppendLine();

            // Pretraga fajlova u root direktorijumu
            if (files.Length > 0)
            {
                responseBuilder.AppendLine("<ul>");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    responseBuilder.AppendLine($"<li><a href=\"http://localhost:5050/{fileName}\">{fileName}</a></li>");
                }
                responseBuilder.AppendLine("</ul>");
            }
            else
            {
                responseBuilder.AppendLine("<p>No files found.</p>");
            }

            // Slanje odgovora klijentu
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
}
