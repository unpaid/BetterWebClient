using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace unpaid
{
    public class BetterWebClient
    {
        private HttpClient Client;
        private int TotalDownloads;

        public class Response
        {
            public Dictionary<string, string> Headers;
            public string Data;
            public string Error;
        }

        public class DownloadProgress
        {
            public int DownloadID;
            public string DownloadPath;

            public int BytesDownloaded;
            public long ContentLength;
            public long TotalBytesDownloaded;
        }
        public delegate void DownloadProgressChanged(DownloadProgress Progress);
        public event DownloadProgressChanged DownloadProgressChangedEvent;

        public BetterWebClient(CookieContainer Cookies = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            Client = new HttpClient(new WebRequestHandler
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable),
                CookieContainer = Cookies ?? new CookieContainer()
            });

            Client.Timeout = Timeout.InfiniteTimeSpan;

            Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-AU,en-GB,en-US,en");
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.87 Safari/537.36");
        }

        public Response Request(string URL, HttpMethod Method, Dictionary<string, string> Data = null, Dictionary<string, string> Headers = null)
        {
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Get)
                {
                    if (Data != null)
                    {
                        NameValueCollection Collection = HttpUtility.ParseQueryString(String.Empty);
                        foreach (KeyValuePair<string, string> Pair in Data)
                        {
                            Collection[Pair.Key] = Pair.Value;
                        }

                        URL = $"{URL}?{Collection.ToString()}";
                    }
                }
                else if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = new FormUrlEncodedContent(Data);
                    }
                }

                RequestMessage.RequestUri = new Uri(URL, UriKind.Absolute);

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = Client.SendAsync(RequestMessage).Result)
                {
                    if (!ResponseMessage.IsSuccessStatusCode)
                    {
                        return new Response
                        {
                            Error = $"ERROR: {URL}: {ResponseMessage.StatusCode} - {ResponseMessage.ReasonPhrase}"
                        };
                    }
                    return new Response
                    {
                        Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                        Data = ResponseMessage.Content.ReadAsStringAsync().Result,
                        Error = String.Empty
                    };
                }
            }
        }

        public async Task<Response> RequestAsync(string URL, HttpMethod Method, Dictionary<string, string> Data = null, Dictionary<string, string> Headers = null)
        {
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Get)
                {
                    if (Data != null)
                    {
                        NameValueCollection Collection = HttpUtility.ParseQueryString(String.Empty);
                        foreach (KeyValuePair<string, string> Pair in Data)
                        {
                            Collection[Pair.Key] = Pair.Value;
                        }

                        URL = $"{URL}?{Collection.ToString()}";
                    }
                }
                else if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = new FormUrlEncodedContent(Data);
                    }
                }

                RequestMessage.RequestUri = new Uri(URL, UriKind.Absolute);

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = await Client.SendAsync(RequestMessage))
                {
                    if (!ResponseMessage.IsSuccessStatusCode)
                    {
                        return new Response
                        {
                            Error = $"ERROR: {URL}: {ResponseMessage.StatusCode} - {ResponseMessage.ReasonPhrase}"
                        };
                    }
                    return new Response
                    {
                        Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                        Data = await ResponseMessage.Content.ReadAsStringAsync(),
                        Error = String.Empty
                    };
                }
            }
        }

        public Response DownloadFile(string URL, HttpMethod Method, string FilePath, Dictionary<string, string> Data = null, Dictionary<string, string> Headers = null)
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePath);
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Get)
                {
                    if (Data != null)
                    {
                        NameValueCollection Collection = HttpUtility.ParseQueryString(String.Empty);
                        foreach (KeyValuePair<string, string> Pair in Data)
                        {
                            Collection[Pair.Key] = Pair.Value;
                        }

                        URL = $"{URL}?{Collection.ToString()}";
                    }
                }
                else if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = new FormUrlEncodedContent(Data);
                    }
                }

                RequestMessage.RequestUri = new Uri(URL, UriKind.Absolute);

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = Client.SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    if (!ResponseMessage.IsSuccessStatusCode)
                    {
                        return new Response
                        {
                            Error = $"ERROR: {URL}: {ResponseMessage.StatusCode} - {ResponseMessage.ReasonPhrase}"
                        };
                    }

                    long ContentLength = 0;
                    int CurrentDownloadID = TotalDownloads++;
                    if (ResponseMessage.Content.Headers.TryGetValues("Content-Length", out IEnumerable<String> Values))
                    {
                        Int64.TryParse(Values.First(), out ContentLength);
                    }

                    using (Stream ContentStream = ResponseMessage.Content.ReadAsStreamAsync().Result)
                    {
                        string FolderPath = Path.GetDirectoryName(FilePath);
                        if (!Directory.Exists(FolderPath))
                        {
                            Directory.CreateDirectory(FolderPath);
                        }
                        if (File.Exists(FilePath))
                        {
                            File.Delete(FilePath);
                        }

                        // 1 Megabyte Max Chunk Size
                        int MaxChunkSize = 1 * 1000 * 1000;
                        byte[] Buffer = new byte[MaxChunkSize];
                        long TotalNumberOfBytesRead = 0;

                        using (FileStream FileStream = File.Create(FilePath))
                        {
                            int NumberOfBytesRead = ContentStream.Read(Buffer, 0, Buffer.Length);
                            do
                            {
                                FileStream.Write(Buffer, 0, NumberOfBytesRead);

                                TotalNumberOfBytesRead += NumberOfBytesRead;
                                OnDownloadProgressChanged(new DownloadProgress
                                {
                                    DownloadID = CurrentDownloadID,
                                    DownloadPath = FilePath,
                                    TotalBytesDownloaded = TotalNumberOfBytesRead,
                                    ContentLength = ContentLength,
                                    BytesDownloaded = NumberOfBytesRead
                                });

                            } while ((NumberOfBytesRead = ContentStream.Read(Buffer, 0, Buffer.Length)) != 0);
                        }

                        return new Response
                        {
                            Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                            Data = FilePath,
                            Error = String.Empty
                        };
                    }
                }
            }
        }

        public async Task<Response> DownloadFileAsync(string URL, HttpMethod Method, string FilePath, Dictionary<string, string> Data = null, Dictionary<string, string> Headers = null)
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePath);
            using (HttpRequestMessage RequestMessage = new HttpRequestMessage() { Method = Method })
            {
                if (Method == HttpMethod.Get)
                {
                    if (Data != null)
                    {
                        NameValueCollection Collection = HttpUtility.ParseQueryString(String.Empty);
                        foreach (KeyValuePair<string, string> Pair in Data)
                        {
                            Collection[Pair.Key] = Pair.Value;
                        }

                        URL = $"{URL}?{Collection.ToString()}";
                    }
                }
                else if (Method == HttpMethod.Post)
                {
                    if (Data != null)
                    {
                        RequestMessage.Content = new FormUrlEncodedContent(Data);
                    }
                }

                RequestMessage.RequestUri = new Uri(URL, UriKind.Absolute);

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> Header in Headers)
                    {
                        RequestMessage.Headers.Add(Header.Key, Header.Value);
                    }
                }

                using (HttpResponseMessage ResponseMessage = await Client.SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!ResponseMessage.IsSuccessStatusCode)
                    {
                        return new Response
                        {
                            Error = $"ERROR: {URL}: {ResponseMessage.StatusCode} - {ResponseMessage.ReasonPhrase}"
                        };
                    }

                    long ContentLength = 0;
                    int CurrentDownloadID = TotalDownloads++;
                    if (ResponseMessage.Content.Headers.TryGetValues("Content-Length", out IEnumerable<String> Values))
                    {
                        Int64.TryParse(Values.First(), out ContentLength);
                    }

                    using (Stream ContentStream = await ResponseMessage.Content.ReadAsStreamAsync())
                    {
                        string FolderPath = Path.GetDirectoryName(FilePath);
                        if (!Directory.Exists(FolderPath))
                        {
                            Directory.CreateDirectory(FolderPath);
                        }
                        if (File.Exists(FilePath))
                        {
                            File.Delete(FilePath);
                        }

                        // 1 Megabyte Max Chunk Size
                        int MaxChunkSize = 1 * 1000 * 1000;
                        byte[] Buffer = new byte[MaxChunkSize];
                        long TotalNumberOfBytesRead = 0;

                        using (FileStream FileStream = File.Create(FilePath))
                        {
                            int NumberOfBytesRead = await ContentStream.ReadAsync(Buffer, 0, Buffer.Length);
                            do
                            {
                                await FileStream.WriteAsync(Buffer, 0, NumberOfBytesRead);

                                TotalNumberOfBytesRead += NumberOfBytesRead;
                                OnDownloadProgressChanged(new DownloadProgress
                                {
                                    DownloadID = CurrentDownloadID,
                                    DownloadPath = FilePath,
                                    TotalBytesDownloaded = TotalNumberOfBytesRead,
                                    ContentLength = ContentLength,
                                    BytesDownloaded = NumberOfBytesRead
                                });

                            } while ((NumberOfBytesRead = await ContentStream.ReadAsync(Buffer, 0, Buffer.Length)) != 0);
                        }

                        return new Response
                        {
                            Headers = ResponseMessage.Headers.Concat(ResponseMessage.Content.Headers).ToDictionary(x => x.Key, x => String.Join(", ", x.Value).TrimEnd(' ')),
                            Data = FilePath,
                            Error = String.Empty
                        };
                    }
                }
            }
        }

        private void OnDownloadProgressChanged(DownloadProgress Progress)
        {
            DownloadProgressChangedEvent?.Invoke(Progress);
        }
    }
}
