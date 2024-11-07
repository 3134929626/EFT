using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ComponentAce.Compression.Libs.zlib;
using Newtonsoft.Json;
using OpenTK.Input;
using RestSharp;
//using SharpCompress.Compressors.Deflate;


namespace eft_dma_radar
{
    public class ProfileAPI : IDisposable
    {
        private readonly string _phpSessionId;
        private readonly HttpClient _httpClient;

        private static readonly byte[] GAME_ENCRYPTION_KEY = {
        0x51, 0x6F, 0x2A, 0x6E, 0x70, 0x37, 0x2A, 0x79, 0x50, 0x48, 0x71, 0x57, 0x58, 0x38, 0x5A, 0x42,
        0x33, 0x5A, 0x4F, 0x40, 0x6D, 0x31, 0x6B, 0x34
        };

        private static readonly byte[] GAME_IV = {
        0xF5, 0xDB, 0xE9, 0x2B, 0xEC, 0xED, 0xF3, 0xDD, 0x7F, 0x4B, 0xE4, 0x8D, 0x17, 0xD2, 0x38, 0x7A
        };

        private const string PRODUCTION_URL = "https://prod.escapefromtarkov.com";

        public ProfileAPI(string phpSessionId)
        {
            _phpSessionId = phpSessionId;
            _httpClient = new HttpClient();
            //_httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            //_httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityPlayer/2019.4.39f1 (UnityWebRequest/1.0, libcurl/7.52.0-DEV)");
            //_httpClient.DefaultRequestHeaders.Add("App-Version", "EFT Client 0.15.3.5176");
            //_httpClient.DefaultRequestHeaders.Add("X-Unity-Version", "2019.4.39f1");
            //_httpClient.DefaultRequestHeaders.Add("Cookie", $"PHPSESSID={_phpSessionId}");
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public class Info
        {
            public string Nickname { get; set; }
            public int Experience { get; set; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int InGameTime { get; set; }
        }
    

        public string GetInfo(string accountId, out Info info)
        {
            info = null;
            try
            {
                var client = new RestClient("https://prod.escapefromtarkov.com");

                var request = new RestRequest("/client/profile/view", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("User-Agent", "UnityPlayer/2019.4.39f1 (UnityWebRequest/1.0, libcurl/7.52.0-DEV)");
                request.AddHeader("App-Version", "EFT Client 0.15.3.5176");
                request.AddHeader("X-Unity-Version", "2019.4.39f1");
                request.AddHeader("Cookie", $"PHPSESSID={_phpSessionId}");

                var payload = new { accountId = "98374398" };

                request.AddJsonBody(payload);

                var response = client.Execute(request);
                File.WriteAllText("response.bin", response.Content);
                //Console.WriteLine($"Status code: {response.StatusCode}");

                var responseBytes = response.RawBytes;
                File.WriteAllBytes("response_raw.bin", responseBytes);

                var decrypted = DecryptBytes(GAME_ENCRYPTION_KEY, GAME_IV, responseBytes);
                File.WriteAllBytes("response_decrypted.bin", decrypted);

                var decompressed = ZLibDotnetDecompress(decrypted);

                //Console.WriteLine($"Content: {decompressed}");
                //Console.ReadLine();

                //var data = JsonConvert.SerializeObject(new { accountId });
                //string data = "{\"accountId\":\"" + accountId + "\"}";
                //var response = SendRequest("/client/profile/view", data);
                return response.Content;

                var root = JsonConvert.DeserializeObject<dynamic>("response");
                if (root["err"] != 0)
                {
                    return "false";
                }

                var dataNode = root["data"];
                var infoNode = dataNode["info"];
                var pmcStatsNode = dataNode["pmcStats"]["eft"]["overAllCounters"]["Items"];

                info = new Info
                {
                    Nickname = infoNode["nickname"].ToString(),
                    Experience = (int)infoNode["experience"],
                    Kills = (int)pmcStatsNode.First["Value"],
                    Deaths = (int)pmcStatsNode.Last["Value"],
                    InGameTime = (int)dataNode["pmcStats"]["eft"]["totalInGameTime"]
                };
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                return ex.Message;
            }

                return "true";
        }

        private string SendRequest(string url, string data)
        {
            //_httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityPlayer/2019.4.39f1 (UnityWebRequest/1.0, libcurl/7.52.0-DEV)");
            _httpClient.DefaultRequestHeaders.Add("App-Version", "EFT Client 0.15.3.5176");
            _httpClient.DefaultRequestHeaders.Add("X-Unity-Version", "2019.4.39f1");
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"PHPSESSID={_phpSessionId}");


            var response = _httpClient.PostAsync(PRODUCTION_URL + url, new StringContent(data, Encoding.UTF8, "application/json"));
            response.Wait();
            var response1 = response.Result;

            if (!response1.IsSuccessStatusCode)
            {
                throw new Exception($"Request failed: {response1.ReasonPhrase}");
            }

            var responseBytes = response1.Content.ReadAsByteArrayAsync();
            responseBytes.Wait();
            var responseBytes1 = responseBytes.Result;
            var decrypted = DecryptBytes(GAME_ENCRYPTION_KEY, GAME_IV, responseBytes1);
            //var deflatedContent = zlibStream.Decompress(decrypted, null);
            return ZLibDotnetDecompress(decrypted);
        }
        
        private static byte[] DecryptData(byte[] key, byte[] iv, byte[] cipherText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    return PerformCryptography(cipherText, decryptor);
                }
            }
        }

        public static string ZLibDotnetDecompress(byte[] data)
        {
            using (MemoryStream compressed = new MemoryStream(data))
            using (ZInputStream inputStream = new ZInputStream(compressed))
            using (MemoryStream outputStream = new MemoryStream())
            {
                byte[] buffer = new byte[1024]; // 定义缓冲区
                int bytesRead;

                // 循环读取直到流结束
                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead); // 将读取的字节写入输出流
                }

                // 将解压缩后的字节转换为字符串
                return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }

        private static byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }
        public static byte[] DecryptBytes(byte[] key, byte[] iv, byte[] cipherText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None; // No padding

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var msDecrypt = new MemoryStream(cipherText))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var msResult = new MemoryStream())
                {
                    csDecrypt.CopyTo(msResult);
                    return msResult.ToArray();
                }
            }
        }

        public static byte[] DecryptData(byte[] key, byte[] data)
        {
            const int IV_LENGTH = 16;
            byte[] actualIV = new byte[IV_LENGTH];
            Array.Copy(data, actualIV, IV_LENGTH);

            byte[] actualData = new byte[data.Length - IV_LENGTH];
            Array.Copy(data, IV_LENGTH, actualData, 0, actualData.Length);

            return DecryptBytes(actualData, key, actualIV);
        }


    }
}

