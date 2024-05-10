using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebServer
{
    class Program
    {
        static Dictionary<string, string> responseCache = new Dictionary<string, string>();
        static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        static void Main(string[] args)
        {
            TcpListener server = null;
            try
            {
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                int port = 5050;
                server = new TcpListener(ipAddress, port);
                server.Start();

                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
            finally
            {
                server.Stop();
            }
        }

        static async Task HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            Console.WriteLine($"Received request: {request}");

            string keyword = GetKeywordFromRequest(request);

            string response;
            await semaphore.WaitAsync();
            try
            {
                if (responseCache.ContainsKey(keyword))
                {
                    response = responseCache[keyword];
                    Console.WriteLine($"Response for keyword '{keyword}' found in cache.");
                }
                else
                {
                    response = await GetFilesByKeyword(keyword);
                    responseCache[keyword] = response;
                    Console.WriteLine($"Response for keyword '{keyword}' cached.");
                }
            }
            finally
            {
                semaphore.Release();
            }

            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

            stream.Close();
            client.Close();
        }

        static string GetKeywordFromRequest(string request)
        {
            return "keyword"; // Placeholder, should parse the request for the keyword
        }

        static async Task<string> GetFilesByKeyword(string keyword)
        {
            try
            {
                string directoryPath = Directory.GetCurrentDirectory();
                var files = Directory.GetFiles(directoryPath, $"*{keyword}*");

                if (files.Length == 0)
                {
                    return "No files found for the keyword.";
                }

                var response = new StringBuilder();
                response.Append("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n\r\n");
                response.Append("<!DOCTYPE html><html><head><title>Search Results</title></head><body><h1>Files found:</h1><ul>");

                foreach (var file in files)
                {
                    
                        var fileName = Path.GetFileName(file);
                        var fileUrl = $"http://localhost:5050/{fileName}";
                        response.Append($"<li><a href=\"{fileUrl}\">{fileName}</a></li>");
                    

                }

                response.Append("</ul></body></html>");
                return response.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}

