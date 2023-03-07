using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace unpaid
{
    public class BetterWebClient
    {
        private HttpClient Client;
        private CookieContainer Cookies;
        // 1 Mebibyte Max Chunk Size
        private readonly int MaxChunkSize = (int)Math.Pow(2, 20);
        public bool SetCookies;

        public class Response
        {
            public HttpStatusCode StatusCode;
            public string ReasonPhrase;
            public Dictionary<string, string> Headers;
            public dynamic Data;
            public bool IsSuccessStatusCode;
        }

        public class DownloadProgress
        {
            public string DownloadURL;
            public string DownloadPath;

            public int BytesDownloaded;
            public long ContentLength;
            public long TotalBytesDownloaded;
        }

        public BetterWebClient(Uri ProxyAddress = null, string ProxyUsername = null, string ProxyPassword = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

            Cookies = new CookieContainer();

            HttpClientHandler RequestHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                CookieContainer = Cookies,
                UseCookies = true
            };

            if (ProxyAddress != null)
            {
                WebProxy Proxy = new WebProxy()
                {
                    Address = ProxyAddress,
                    BypassProxyOnLocal = true,
                    UseDefaultCredentials = false
                };
                if (!String.IsNullOrEmpty(ProxyUsername) && !String.IsNullOrEmpty(ProxyPassword))
                {
                    Proxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);
                    RequestHandler.UseDefaultCredentials = false;
                    RequestHandler.PreAuthenticate = true;
                }
                RequestHandler.Proxy = Proxy;
                RequestHandler.UseProxy = true;
            }

            Client = new HttpClient(RequestHandler)
            {
                Timeout = TimeSpan.FromMinutes(1)
            };

            Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-AU,en-GB,en-US,en");
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.87 Safari/537.36");
            Client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        }

        public void AddCookie(string Name, string Value, string Domain)
        {
            Cookies.Add(new Cookie(Name, Value, "/", Domain));
        }

        public void ClearCookies()
        {
            Cookies = new CookieContainer();
        }

        public Response Request(string URL, HttpMethod Method, IEnumerable<KeyValuePair<string, string>> Params = null, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null)
        {
            if (Params != null)
            {
                URL = $"{URL}?{new FormUrlEncodedContent(Params).ReadAsStringAsync().Result}";
            }

            return Request(new Uri(URL, UriKind.Absolute), Method, Data, Headers);
        }
        public Response Request(Uri URL, HttpMethod Method, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null)
        {
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = Data;
                    }
                }

                RequestMessage.RequestUri = URL;

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = Client.SendAsync(RequestMessage).Result)
                {
                    Response response = new Response
                    {
                        StatusCode = ResponseMessage.StatusCode,
                        ReasonPhrase = ResponseMessage.ReasonPhrase,
                        Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                        IsSuccessStatusCode = ResponseMessage.IsSuccessStatusCode
                    };
                    if (ResponseMessage.Content.Headers.ContentType.MediaType.Contains("octet-stream"))
                        response.Data = ResponseMessage.Content.ReadAsByteArrayAsync().Result;
                    else
                        response.Data = Encoding.UTF8.GetString(ResponseMessage.Content.ReadAsByteArrayAsync().Result);

                    if (SetCookies && ResponseMessage.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> setCookies))
                    {
                        foreach (string setCookie in setCookies)
                        {
                            string Name   = Regex.Match(setCookie, "^[^=]*").Value;
                            string Value  = Regex.Match(setCookie, $"^{Name}=([^;]*)").Groups[1].Value;
                            string Path   = Regex.Match(setCookie, "path=([^;]*)").Groups[1].Value;
                            string Domain = Regex.Match(setCookie, "domain=([^;]*)").Groups[1].Value;
                            Cookies.Add(new Cookie(Name, Value, Path, String.IsNullOrEmpty(Domain) ? RequestMessage.RequestUri.Host : Domain));
                        }
                    }

                    return response;
                }
            }
        }

        public async Task<Response> RequestAsync(string URL, HttpMethod Method, IEnumerable<KeyValuePair<string, string>> Params = null, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null)
        {
            if (Params != null)
            {
                URL = $"{URL}?{await new FormUrlEncodedContent(Params).ReadAsStringAsync()}";
            }

            return await RequestAsync(new Uri(URL, UriKind.Absolute), Method, Data, Headers);
        }
        public async Task<Response> RequestAsync(Uri URL, HttpMethod Method, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null)
        {
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = Data;
                    }
                }

                RequestMessage.RequestUri = URL;

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = await Client.SendAsync(RequestMessage))
                {
                    Response response = new Response
                    {
                        StatusCode = ResponseMessage.StatusCode,
                        ReasonPhrase = ResponseMessage.ReasonPhrase,
                        Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                        IsSuccessStatusCode = ResponseMessage.IsSuccessStatusCode
                    };
                    if (ResponseMessage.Content.Headers.ContentType.MediaType.Contains("octet-stream"))
                        response.Data = await ResponseMessage.Content.ReadAsByteArrayAsync();
                    else
                        response.Data = Encoding.UTF8.GetString(await ResponseMessage.Content.ReadAsByteArrayAsync());

                    if (SetCookies && ResponseMessage.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> setCookies))
                    {
                        foreach (string setCookie in setCookies)
                        {
                            string Name   = Regex.Match(setCookie, "^[^=]*").Value;
                            string Value  = Regex.Match(setCookie, $"^{Name}=([^;]*)").Groups[1].Value;
                            string Path   = Regex.Match(setCookie, "path=([^;]*)").Groups[1].Value;
                            string Domain = Regex.Match(setCookie, "domain=([^;]*)").Groups[1].Value;
                            Cookies.Add(new Cookie(Name, Value, Path, String.IsNullOrEmpty(Domain) ? RequestMessage.RequestUri.Host : Domain));
                        }
                    }

                    return response;
                }
            }
        }

        public Response DownloadFile(string URL, HttpMethod Method, string FilePath, IEnumerable<KeyValuePair<string, string>> Params = null, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null, Action<DownloadProgress> ProgressCallback = null, CancellationToken Token = default(CancellationToken))
        {
            if (Params != null)
            {
                URL = $"{URL}?{new FormUrlEncodedContent(Params).ReadAsStringAsync().Result}";
            }

            return DownloadFile(new Uri(URL, UriKind.Absolute), Method, FilePath, Data, Headers, ProgressCallback, Token);
        }
        public Response DownloadFile(Uri URL, HttpMethod Method, string FilePath, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null, Action<DownloadProgress> ProgressCallback = null, CancellationToken Token = default(CancellationToken))
        {
            FilePath = SanitizePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePath));
            string FolderPath = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = Data;
                    }
                }

                RequestMessage.RequestUri = URL;

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                long FileSize = 0;
                if (File.Exists(FilePath))
                {
                    FileSize = new FileInfo(FilePath).Length;
                    RequestMessage.Headers.Range = new RangeHeaderValue(FileSize, null);
                }

                using (HttpResponseMessage ResponseMessage = Client.SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    long ContentLength = 0;
                    if (ResponseMessage.Content.Headers.TryGetValues("Content-Length", out IEnumerable<string> Values))
                    {
                        Int64.TryParse(Values.First(), out ContentLength);
                    }
                    ContentLength += FileSize;

                    using (Stream ContentStream = ResponseMessage.Content.ReadAsStreamAsync().Result)
                    {
                        byte[] Buffer = new byte[MaxChunkSize];
                        long TotalNumberOfBytesRead = 0 + FileSize;

                        using (FileStream FileStream = File.Open(FilePath, FileMode.Append | FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            int NumberOfBytesRead = ContentStream.Read(Buffer, 0, Buffer.Length);
                            do
                            {
                                FileStream.Write(Buffer, 0, NumberOfBytesRead);

                                TotalNumberOfBytesRead += NumberOfBytesRead;
                                ProgressCallback?.Invoke(new DownloadProgress
                                {
                                    DownloadURL = URL.OriginalString,
                                    DownloadPath = FilePath,
                                    TotalBytesDownloaded = TotalNumberOfBytesRead,
                                    ContentLength = ContentLength,
                                    BytesDownloaded = NumberOfBytesRead
                                });

                                if (Token.IsCancellationRequested)
                                {
                                    return new Response
                                    {
                                        ReasonPhrase = "Cancelled"
                                    };
                                }

                            } while ((NumberOfBytesRead = ContentStream.Read(Buffer, 0, Buffer.Length)) != 0);
                        }

                        return new Response
                        {
                            StatusCode = ResponseMessage.StatusCode,
                            ReasonPhrase = ResponseMessage.ReasonPhrase,
                            Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                            Data = FilePath,
                            IsSuccessStatusCode = ResponseMessage.IsSuccessStatusCode
                        };
                    }
                }
            }
        }

        public async Task<Response> DownloadFileAsync(string URL, HttpMethod Method, string FilePath, IEnumerable<KeyValuePair<string, string>> Params = null, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null, Action<DownloadProgress> ProgressCallback = null, CancellationToken Token = default(CancellationToken))
        {
            if (Params != null)
            {
                URL = $"{URL}?{await new FormUrlEncodedContent(Params).ReadAsStringAsync()}";
            }

            return await DownloadFileAsync(new Uri(URL, UriKind.Absolute), Method, FilePath, Data, Headers, ProgressCallback, Token);
        }
        public async Task<Response> DownloadFileAsync(Uri URL, HttpMethod Method, string FilePath, HttpContent Data = null, IEnumerable<KeyValuePair<string, string>> Headers = null, Action<DownloadProgress> ProgressCallback = null, CancellationToken Token = default(CancellationToken))
        {
            FilePath = SanitizePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePath));
            string FolderPath = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = Data;
                    }
                }

                RequestMessage.RequestUri = URL;

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                long FileSize = 0;
                if (File.Exists(FilePath))
                {
                    FileSize = new FileInfo(FilePath).Length;
                    RequestMessage.Headers.Range = new RangeHeaderValue(FileSize, null);
                }

                using (HttpResponseMessage ResponseMessage = await Client.SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead))
                {
                    long ContentLength = 0;
                    if (ResponseMessage.Content.Headers.TryGetValues("Content-Length", out IEnumerable<String> Values))
                    {
                        Int64.TryParse(Values.First(), out ContentLength);
                    }
                    ContentLength += FileSize;

                    using (Stream ContentStream = await ResponseMessage.Content.ReadAsStreamAsync())
                    {
                        byte[] Buffer = new byte[MaxChunkSize];
                        long TotalNumberOfBytesRead = 0 + FileSize;

                        using (FileStream FileStream = File.Open(FilePath, FileMode.Append | FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            int NumberOfBytesRead = await ContentStream.ReadAsync(Buffer, 0, Buffer.Length);
                            do
                            {
                                await FileStream.WriteAsync(Buffer, 0, NumberOfBytesRead);

                                TotalNumberOfBytesRead += NumberOfBytesRead;
                                ProgressCallback?.Invoke(new DownloadProgress
                                {
                                    DownloadURL = URL.OriginalString,
                                    DownloadPath = FilePath,
                                    TotalBytesDownloaded = TotalNumberOfBytesRead,
                                    ContentLength = ContentLength,
                                    BytesDownloaded = NumberOfBytesRead
                                });

                                if (Token.IsCancellationRequested)
                                {
                                    return new Response
                                    {
                                        ReasonPhrase = "Cancelled"
                                    };
                                }

                            } while ((NumberOfBytesRead = await ContentStream.ReadAsync(Buffer, 0, Buffer.Length)) != 0);
                        }

                        return new Response
                        {
                            StatusCode = ResponseMessage.StatusCode,
                            ReasonPhrase = ResponseMessage.ReasonPhrase,
                            Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                            Data = FilePath,
                            IsSuccessStatusCode = ResponseMessage.IsSuccessStatusCode
                        };
                    }
                }
            }
        }

        private string SanitizePath(string FilePath)
        {
            string Root = Path.GetPathRoot(FilePath);
            string FileName = Path.GetFileName(FilePath);
            if (Path.IsPathRooted(FilePath))
                FilePath = FilePath.Replace(Root, String.Empty);
            FilePath = FilePath.Replace(":", String.Empty);
            FilePath = FilePath.Replace(FileName, String.Empty);
            foreach (char Invalid in Path.GetInvalidPathChars())
                FilePath = FilePath.Replace(Invalid.ToString(), String.Empty);
            foreach (char Invalid in Path.GetInvalidFileNameChars())
                FileName = FileName.Replace(Invalid.ToString(), String.Empty);
            FilePath = Path.Combine(Root, FilePath, FileName);

            return FilePath;
        }
    }
}
