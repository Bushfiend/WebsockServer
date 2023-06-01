


namespace WebsockServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var server = new WebsocketServer("http://192.168.0.43:9900/");
            await server.StartServerAsync();

            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadLine();

            server.StopServer();
        }
    }
}