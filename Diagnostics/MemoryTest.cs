using System.Diagnostics;
using AxarDB;
using AxarDB.Definitions;
using Microsoft.Extensions.Caching.Memory;
using AxarDB.Storage;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using AxarDB.Core;

namespace AxarDB.Diagnostics
{
    public static class MemoryTest
{
    public static void Run()
    {
        Console.WriteLine("Memory and Lazy Load C# Test Starting...");
        
        if (Directory.Exists("TestData")) Directory.Delete("TestData", true);
        
        var storage = new DiskStorage("TestData");
        var cacheOptions = new MemoryCacheOptions { SizeLimit = 10 * 1024 * 1024 }; // 10MB limit for test cache
        var cache = new MemoryCache(cacheOptions);
        
        var collection = new Collection("CSharpMemTest", storage, cache);
        
        // populate
        if (collection.FindAll().Take(1).Count() == 0)
        {
            Console.WriteLine("Seeding 50,000 documents to trigger lazy load impact...");
            var dicts = new Dictionary<string, object>[50000];
            for (int i=0; i<50000; i++)
            {
                dicts[i] = new Dictionary<string, object> {
                    { "_id", AxarDB.Helpers.GuidV7.NewGuid().ToString() },
                    { "id", i },
                    { "name", "Test " + i },
                    { "data", new string('x', 500) } 
                };
            }
            
            storage.EnsureCollection("CSharpMemTest");
            // Fast insert avoiding cache hits to disk
            foreach(var d in dicts) {
                 storage.SaveDocument("CSharpMemTest", d);
            }
            collection.Reload();
        }

        Console.WriteLine("Seed Complete. Force cleaning memory...");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startMem = GC.GetTotalMemory(true);
        Console.WriteLine($"Memory Before Query: {startMem / 1024 / 1024} MB");

        var sw = Stopwatch.StartNew();
        
        // Lazy Evaluation Test
        Console.WriteLine("Running lazy take(5) fetch...");
        
        // Since FindAll returns IEnumerable via PLINQ, running Take(5) should ideally not evaluate all 50,000.
        // It will fetch from disk concurrently but stop as soon as it has 5 items.
        var results = collection.FindAll().Take(5).ToList();
        
        sw.Stop();
        long endMem = GC.GetTotalMemory(true);
        
        Console.WriteLine($"Query Time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Selected items count: {results.Count}");
        Console.WriteLine($"Memory After Query: {endMem / 1024 / 1024} MB");
        Console.WriteLine($"Memory growth during query: {(endMem - startMem) / 1024} KB");

        if (sw.ElapsedMilliseconds > 1500)
        {
            Console.WriteLine("WARNING: Query took too long. Lazy evaluation mechanism might be evaluating everything first.");
        }
        else
        {
            Console.WriteLine("SUCCESS: Query completed rapidly, implying lazy evaluation worked. Not all data was materialized.");
        }
        
        Console.WriteLine("Test Finished.");
    }
}
}
