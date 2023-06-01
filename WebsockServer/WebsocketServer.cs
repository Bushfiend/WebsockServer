using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebsockServer
{
    public class WebsocketServer
    {

        //Jonah is a cuck.


        private HttpListener? listener;
        private CancellationTokenSource? cancellationToken;
        private Dictionary<string, List<WebSocket>>? eventSubscriptions;
        private string URL { get; set; }

        public WebsocketServer(string url)
        {
            eventSubscriptions = new Dictionary<string, List<WebSocket>>();
            URL = url;
        }

        public Task StartServerAsync()
        {

            if(!URL.Contains("http://") && !URL.EndsWith('/'))
            {
                ConsoleWrite("Server URL invalid.");
                return Task.CompletedTask;
            }
            if (_verbose)
                ConsoleWrite("Verbose Mode Enabled!");


            listener = new HttpListener();

            listener.Prefixes.Add(URL);
           
            listener.Start();
            if (_verbose)
                ConsoleWrite("Listener started.");

            cancellationToken = new CancellationTokenSource();

            ConsoleWrite("WebSocket server started.");
            AcceptConnections();
            if (_verbose)
                ConsoleWrite("Waiting for connections.");

            return Task.CompletedTask;
        }

        private async void AcceptConnections()
        {

            if(cancellationToken == null || listener == null)
            {
                ConsoleWrite("Something went wrong. cancellationToken or listener didnt intialize.. why?.");
                return;
            }

            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        ProcessWebSocketRequest(context);

                        if (_verbose)
                            ConsoleWrite($"Client connected from {context.Request.RemoteEndPoint}");
                    }
                    else
                    {
                        if (_verbose)
                            ConsoleWrite($"Client failed to connect. Status code: {context.Response.StatusCode}");

                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch(Exception ex)
                {
                    ConsoleWrite($"Error: {ex}");
                    listener.Stop();
                }
                
            }
        }

        private async void ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocket webSocket;
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            webSocket = webSocketContext.WebSocket;

            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await HandleWebSocketConnection(webSocket);
                }
                catch
                {
                    ConsoleWrite($"Client disconnected.");
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

        private void UnsubscribeDisconnectedClient(WebSocket client)
        {
            if(eventSubscriptions == null)
            {
                ConsoleWrite($"eventSubscriptions was not initialized...");
                return;
            }

            foreach (var eventSub in eventSubscriptions)
            {
               if(eventSub.Value.Contains(client))
                {
                    eventSub.Value.Remove(client);
                    if(_verbose)
                        ConsoleWrite($"Client unsubscribed to '{eventSub.Key}'.");
                }        
            }
        }

        private async Task HandleWebSocketConnection(WebSocket client)
        {
            var buffer = new byte[1024];
            while (client.State == WebSocketState.Open)
            {
                var receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var data = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                    var socketMessage = SocketMessage.Deserialize(data);
                  
                    switch (socketMessage.Command)
                    {
                        case SocketMessage.CommandType.Subscribe:
                            SubscribeToEvent(client, socketMessage.EventName);
                            break;
                        case SocketMessage.CommandType.Unsubscribe:
                            UnsubscribeFromEvent(client, socketMessage.EventName);
                            break;
                        default:
                        await HandleEventMessage(client, socketMessage.EventName, socketMessage.Message);
                            break;
                            
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }

        private async Task HandleEventMessage(WebSocket client, string eventName, string message)
        {
            if (eventSubscriptions == null)
            {
                ConsoleWrite($"eventSubscriptions was not initialized...");
                return;
            }

            if(_verbose)
                ConsoleWrite($"Received event message on '{eventName}': {message}");


            if (eventSubscriptions.TryGetValue(eventName, out List<WebSocket>? subscribers))
            {
                if (subscribers == null)
                    return;

                var socketMessage = new SocketMessage(SocketMessage.CommandType.SendMessage, eventName, message);

                var buffer = Encoding.UTF8.GetBytes(socketMessage.Serialize());
                var segment = new ArraySegment<byte>(buffer);

                foreach (var subscriber in subscribers)
                {
                    if (subscriber != client)
                    {
                        await subscriber.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

                if (_verbose && subscribers.Count > 1)
                    ConsoleWrite($"Event message relayed to {subscribers.Count - 1} subscribers.");
            }
        }



        private void SubscribeToEvent(WebSocket client, string eventName)
        {
            if (eventSubscriptions == null)
            {
                ConsoleWrite($"eventSubscriptions was not initialized...");
                return;
            }

            if (!eventSubscriptions.ContainsKey(eventName))
            {
                eventSubscriptions[eventName] = new List<WebSocket>();
            }

            if (!eventSubscriptions[eventName].Contains(client))
            {
                eventSubscriptions[eventName].Add(client);
                ConsoleWrite($"Client subscribed to event '{eventName}'.");
            }
            else
            {
                ConsoleWrite($"Client is already subscribed to event '{eventName}'.");
            }
        }



        private void UnsubscribeFromEvent(WebSocket client, string eventName)
        {
            if (eventSubscriptions == null)
            {
                ConsoleWrite($"eventSubscriptions was not initialized...");
                return;
            }

            if (eventSubscriptions.ContainsKey(eventName))
            {
                eventSubscriptions[eventName].Remove(client);
                         
                if(_verbose)
                {
                    ConsoleWrite($"Client unsubscribed from event '{eventName}'.");
                    ConsoleWrite($"Total number of subscribers for event '{eventName}': {eventSubscriptions[eventName].Count}");
                }       
            }
        }

        // Send Message to subscribers serverside
        public async Task PublishEventAsync(SocketMessage message)
        {
            if (eventSubscriptions == null)
            {
                return;
            }


            if (eventSubscriptions.ContainsKey(message.EventName))
            {
                var subscribers = eventSubscriptions[message.EventName];

                var buffer = Encoding.UTF8.GetBytes(message.Serialize());
                var segment = new ArraySegment<byte>(buffer);

                foreach (var subscriber in subscribers)
                {
                    await subscriber.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }

                if (_verbose)
                    ConsoleWrite($"Event '{message.EventName}' published to {subscribers.Count} subscribers.");
            }
        }



        public void StopServer()
        {
            if (cancellationToken == null || listener == null)
            {
                ConsoleWrite("Server is already stopped.");
                return;
            }
                
            cancellationToken.Cancel();
            listener.Stop();
            listener.Close();
            
            ConsoleWrite("Server shutting down.");
        }

        
        public bool VerboseMode
        {
            get
            {
                return _verbose;
            }
            set
            {
                _verbose = value;
            }
        }

        public static void ConsoleWrite(string message)
        {
            Console.WriteLine(message);
        }

        private bool _verbose = true;


    }
}
