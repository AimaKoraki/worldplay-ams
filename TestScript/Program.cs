using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Supabase;
using WorldplayAMS.Core.Models;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);
            
        var config = builder.Build();
        
        var url = config["Supabase:Url"];
        var key = config["Supabase:Key"];
        
        Console.WriteLine($"URL: {url}");
        
        var options = new SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = true };
        var client = new Supabase.Client(url, key, options);
        await client.InitializeAsync();
        
        var sessions = await client.From<Session>()
            .Select("*")
            .Order("starttime", Postgrest.Constants.Ordering.Descending)
            .Limit(5)
            .Get();
            
        foreach(var s in sessions.Models)
        {
            Console.WriteLine($"Session {s.Id}:");
            Console.WriteLine($"  Raw StartTime: {s.StartTime:O} (Kind: {s.StartTime.Kind})");
            Console.WriteLine($"  Raw EndTime:   {(s.EndTime.HasValue ? s.EndTime.Value.ToString("O") : "null")} (Kind: {(s.EndTime.HasValue ? s.EndTime.Value.Kind.ToString() : "N/A")})");
            Console.WriteLine($"  Duration:      {s.TotalDurationMinutes} min");
            Console.WriteLine($"  Fee:           {s.Fee}");
        }
    }
}
