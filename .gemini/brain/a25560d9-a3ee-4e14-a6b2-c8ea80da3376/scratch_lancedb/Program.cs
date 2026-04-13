using System;
using System.Threading.Tasks;
using LanceDB;

class Program
{
    static async Task Main()
    {
        var connection = await Connection.ConnectAsync("data");
        Console.WriteLine("Connected to LanceDB!");
    }
}
