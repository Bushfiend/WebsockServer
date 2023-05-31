using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebsockServer
{
    public class SocketMessage
    {
        public enum CommandType { Subscribe = 0, Unsubscribe = 1, SendMessage = 2 };

        public CommandType Command { get; private set; }
        public string EventName { get; private set; }
        public string Message { get; private set; }

        public SocketMessage(CommandType command, string eventName, string message)
        {
            Command = command;
            EventName = eventName;
            Message = message;
        }

        public string Serialize()
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

            string jsonData = JsonSerializer.Serialize(this, jsonOptions);
            return jsonData;
        }

        public static SocketMessage Deserialize(string jsonData)
        {
           var socketMessage = JsonSerializer.Deserialize<SocketMessage>(jsonData);

            if (socketMessage == null)
                return new SocketMessage(CommandType.SendMessage, "Null", "Null");

            return socketMessage;
        }

    }
}
