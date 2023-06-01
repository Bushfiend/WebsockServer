


namespace WebsockServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var server = new WebsocketServer("http://172.26.6.77:9900/");
            await server.StartServerAsync();

            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadLine();

            server.StopServer();
        }
    }
}