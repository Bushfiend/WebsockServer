using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net;


namespace WebsockServer
{
    public class Websocket
    {
        private HttpListener? listener;
        private CancellationTokenSource? cancellationToken;
        private Dictionary<string, List<WebSocket>>? eventSubscriptions;


        public Websocket()
        {
            eventSubscriptions = new Dictionary<string, List<WebSocket>>();
        }

        public Task StartAsync(string url)
        {
            listener = new HttpListener();

            listener.Prefixes.Add(url);
            listener.Start();

            cancellationToken = new CancellationTokenSource();

            AcceptConnections();

            Console.WriteLine("WebSocket server started.");
            return Task.CompletedTask;
        }

        private async void AcceptConnections()
        {
            if (cancellationToken == null || listener == null)
                return;

            while (!cancellationToken.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    ProcessWebSocketRequest(context);
                    Console.WriteLine($"Client connected from {context.Request.RemoteEndPoint}");
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleEventMessage(WebSocket webSocket, string eventName, string message)
        {
            Console.WriteLine($"Received event message on '{eventName}': {message}");
            if (eventSubscriptions.TryGetValue(eventName, out List<WebSocket> subscribers))
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);

                foreach (var subscriber in subscribers)
                {
                    if (subscriber != webSocket)
                    {
                        await subscriber.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

                Console.WriteLine($"Event message relayed to {subscribers.Count - 1} subscribers.");
            }
            else
            {
                Console.WriteLine($"Event '{eventName}' has no subscribers.");
            }
        }

      

       
        private async void ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocket webSocket = null;
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            webSocket = webSocketContext.WebSocket;

            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await HandleWebSocketConnection(webSocket);
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"Client Disconnected.");
                }
                finally
                {
                    if (webSocket != null && webSocket.State != WebSocketState.Closed)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        UnsubscribeDisconnectedClient(webSocket);
                    }
                }
            }
        }

        private void UnsubscribeDisconnectedClient(WebSocket webSocket)
        {
            foreach (var eventName in eventSubscriptions.Keys)
            {
                eventSubscriptions[eventName].RemoveAll(socket => socket == webSocket);
            }
        }

        private async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    Console.WriteLine("Received message: " + message);

                    var parts = message.Split(new[] { ' ' }, 2);
                    if (parts.Length == 2)
                    {
                        var command = parts[0];
                        var eventName = parts[1];

                        if (command == "SUBSCRIBE")
                        {
                            SubscribeToEvent(webSocket, eventName);
                        }
                        else if (command == "UNSUBSCRIBE")
                        {
                            UnsubscribeFromEvent(webSocket, eventName);
                        }
                        else
                        {
                            await HandleEventMessage(webSocket, command, eventName);
                        }
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }



        private void SubscribeToEvent(WebSocket webSocket, string eventName)
        {
            if (!eventSubscriptions.ContainsKey(eventName))
            {
                eventSubscriptions[eventName] = new List<WebSocket>();
            }

            if (!eventSubscriptions[eventName].Contains(webSocket))
            {
                eventSubscriptions[eventName].Add(webSocket);
                Console.WriteLine($"Client subscribed to event '{eventName}'.");
            }
            else
            {
                Console.WriteLine($"Client is already subscribed to event '{eventName}'.");
            }
        }

        private void UnsubscribeFromEvent(WebSocket webSocket, string eventName)
        {
            if (eventSubscriptions.ContainsKey(eventName))
            {
                eventSubscriptions[eventName].Remove(webSocket);

                Console.WriteLine($"Client unsubscribed from event '{eventName}'.");
                Console.WriteLine($"Total number of subscribers for event '{eventName}': {eventSubscriptions[eventName].Count}");  // Debug
            }
        }

        public async Task PublishEventAsync(string eventName, string message)
        {
            if (eventSubscriptions == null)
            {
                return;
            }

            if (eventSubscriptions.ContainsKey(eventName))
            {
                var subscribers = eventSubscriptions[eventName];

                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);

                foreach (var subscriber in subscribers)
                {
                    await subscriber.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }

                Console.WriteLine($"Event '{eventName}' published to {subscribers.Count} subscribers.");
            }
        }

        public void Stop()
        {
            cancellationToken?.Cancel();
            listener?.Stop();
            listener?.Close();
            Console.WriteLine("WebSocket server stopped.");
        }



    }
}
