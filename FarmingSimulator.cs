using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Newtonsoft.Json;

namespace PooTrucksClient
{
    using FarmID = String;
    using FillType = Int16;

    struct Product
    {
        public FarmID location;
        public string type; // should be FillType once API is updated
        public float amount;
    }

    class FarmingSimulator
    {
        public void Start(Uri uri)
        {
            client.BaseAddress = new Uri(uri, "/api/resources");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Check for a fs19 installation
            var docs_path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path = docs_path + "/My Games/FarmingSimulator2019";
            if (System.IO.Directory.Exists(path))
            {
                Console.WriteLine("Found Farming Simulator 2019 installation: " + path);

                syncSaveFiles();

                fileWatcher = new FileSystemWatcher(path);
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.Filter = "*.xml";
                fileWatcher.IncludeSubdirectories = true;
                fileWatcher.EnableRaisingEvents = true;
                fileWatcher.Changed += onFileChanged;
            }
        }

        private void onFileChanged(object sender, FileSystemEventArgs args)
        {
            Console.WriteLine("File changed: " + args.FullPath);
            lock (sender)
            {
                syncSaveFiles();
            }    
        }

        private void syncSaveFiles()
        {
            if (System.IO.Directory.Exists(path))
            {
                Console.WriteLine("Syncing save files: " + path);

                // Look for save files
                var dir_info = new DirectoryInfo(path);
                var subdirs = dir_info.GetDirectories("savegame?");
                foreach (var subdir_info in subdirs)
                {
                    // Folders always exist, but really only have a save if there is a careerSavegame.xml contained
                    var save_folder = subdir_info.FullName;
                    var save_path = save_folder + "/careerSavegame.xml";
                    if (File.Exists(save_path))
                    {
                        // Read settings from save xml
                        var doc = XDocument.Parse(File.ReadAllText(save_path));
                        var settings = doc.Root.Element("settings");
                        var saveName = settings.Element("savegameName").Value;
                        var map_id = settings.Element("mapId").Value;
                        var player_name = settings.Element("playerName").Value;
                        FarmID id = player_name + "-" + map_id;
                        Console.WriteLine("Found Farming Simulator 2019 Save: " + saveName + " with id: " + id);

                        // read current storage values from items xml
                        var map = LoadProductMapping(save_folder);
                        var farm_storage = new Dictionary<FillType, float>();
                        doc = XDocument.Parse(File.ReadAllText(save_folder + "/items.xml"));
                        foreach (var item in doc.Root.Elements("item"))
                        {
                            var class_name = item.Attribute("className").Value;

                            // SiloPlaceable is generic storage on the users farm
                            if (class_name == "SiloPlaceable")
                            {
                                foreach (var storage in item.Elements("storage"))
                                {
                                    foreach (var node in storage.Elements("node"))
                                    {
                                        var type = map[node.Attribute("fillType").Value];
                                        farm_storage.TryGetValue(type, out var current);
                                        farm_storage[type] = current + float.Parse(node.Attribute("fillLevel").Value);
                                    }
                                }
                            }

                            // SellingStationPlaceable are selling points where the user can deliver stuff
                            else if (class_name == "SellingStationPlaceable")
                            {
                                foreach (var selling_station in item.Elements("sellingStation"))
                                {
                                    foreach (var stats in selling_station.Elements("stats"))
                                    {
                                        var type_string = stats.Attribute("fillType").Value;
                                        var amount = float.Parse(stats.Attribute("received").Value);
                                        if (amount > 0)
                                        {
                                            if (!map.ContainsKey(type_string))
                                            {
                                                map[type_string] = (FillType)(map.Values.Max() + 1);
                                            }
                                            var type = map[stats.Attribute("fillType").Value];
                                            farm_storage.TryGetValue(type, out var current);
                                            farm_storage[type] = current + amount;
                                        }
                                    }
                                }
                            }
                        }

                        // Send to the server
                        foreach (var storage in farm_storage)
                        {
                            var type_str = map.First(x => x.Value == storage.Key).Key;
                            Product product;
                            product.location = id;
                            product.amount = storage.Value;
                            product.type = type_str;
                            setProduct(product);
                        }
                    }
                }
            }
        }

        private Dictionary<string, FillType> LoadProductMapping(string save_folder)
        {
            var map = new Dictionary<string, FillType>();
            var doc = XDocument.Parse(File.ReadAllText(save_folder + "/densityMapHeight.xml"));
            foreach (var mapping in doc.Root.Elements("tipTypeMapping"))
            {
                map[mapping.Attribute("fillType").Value.ToUpper()] = FillType.Parse(mapping.Attribute("index").Value);
            }
            return map;
        }

        private void setProduct(Product product)
        {
            var json = JsonConvert.SerializeObject(product);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = client.PostAsync("", data).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("HTTP request failed: " + response.ReasonPhrase);
                    Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.GetBaseException().ToString());
            }
        }

        static readonly HttpClient client = new HttpClient();
        String path;
        FileSystemWatcher fileWatcher;
    }
}
