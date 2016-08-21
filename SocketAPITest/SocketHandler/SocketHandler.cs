using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace SocketAPITest
{
    public class SocketHandler
    {
        public const int BufferSize = 4096;

        WebSocket socket;

        //very bad way of managing this, should be API in shared storage!
        static ConcurrentBag<WebSocket> _openSockets = new ConcurrentBag<WebSocket>();

        SocketHandler(WebSocket socket)
        {
            this.socket = socket;
        }

        async Task ProcessSocketAction()
        {
            var buffer = new byte[BufferSize];
            var seg = new ArraySegment<byte>(buffer);

            while (socket.State == WebSocketState.Open 
                    || socket.State == WebSocketState.CloseReceived)
            {
                if (socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult incoming = await this.socket.ReceiveAsync(seg, CancellationToken.None);
                    var outgoing = new ArraySegment<byte>(buffer, 0, incoming.Count);
                    await this.socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);
                    await SendConnectedCount();
                }
                else if  ( socket.State == WebSocketState.CloseReceived)
                {
                    await SendConnectedCount();
                    //break out of while to allow connection to continue closing
                    return;
                }


            }
        }

        private static async Task SendConnectedCount()
        {
            string msg = "num connected clients: " + _openSockets.Where(s => s.State == WebSocketState.Open).Count();

            var buf = new ArraySegment<byte>(Encoding.ASCII.GetBytes(msg));
            await Task.WhenAll(
                 _openSockets
                 .Where(s => s.State == WebSocketState.Open)
                 .Select(s => s.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None))
                 );
        }

        static async Task Acceptor(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
                return;

            WebSocket socket = await hc.WebSockets.AcceptWebSocketAsync();
            if (socket!= null)
            {
                var a = socket.State;
                _openSockets.Add(socket);
            }
            var h = new SocketHandler(socket);
            await h.ProcessSocketAction();
        }

        public static void Map(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(SocketHandler.Acceptor);
        }
    }
}
