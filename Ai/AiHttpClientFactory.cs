using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace gta.Ai
{
    // Фабрика HttpClient для AI-запросов.
    // Один общий HttpClientHandler (чтобы не плодить TCP-сокеты), а сами клиенты создаются
    // на каждый запрос. Таймаут НЕ задаётся на клиенте (Timeout = бесконечный) — он управляется
    // per-request через CancellationToken, поэтому у каждого этапа (STT/LLM/TTS) свой бюджет времени.
    public static class AiHttpClientFactory
    {
        private static readonly HttpClientHandler SharedHandler = new HttpClientHandler();

        static AiHttpClientFactory()
        {
            // TLS 1.2/1.3 для HTTPS к OpenAI/Google/ElevenLabs
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        public static HttpClient Create()
        {
            return new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }
    }
}
