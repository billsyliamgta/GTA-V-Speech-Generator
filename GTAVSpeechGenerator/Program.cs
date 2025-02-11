using System.Xml.Linq;
using System.Text.RegularExpressions;
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No files provided. Please drag one or more .oac files onto the executable.");
            Thread.Sleep(5000);
            return;
        }

        for (int i = 0; i < args.Length; i++) // Validate the files to make sure if they are a .OAC
        {
            if (Path.GetExtension(args[i]) != ".oac")
            {
                Console.WriteLine("One or more of the file(s) you provided is not a .oac file.");
                Thread.Sleep(5000);
            }
        }

        // Get input from the developer for the DLC device name
        Console.WriteLine("Enter your DLC device name:");
        string dlcDeviceName = Console.ReadLine();

        if (string.IsNullOrEmpty(dlcDeviceName))
        {
            Console.WriteLine("DLC device name cannot be null or empty.");
            Thread.Sleep(5000);
            return;
        }

        List<string> combinedXorHashes = new List<string>();
        List<string> speakerXorHashes = new List<string>();
        foreach (var filePath in args)
        {
            try
            {
                string fileContent = File.ReadAllText(filePath);
                var waveTrackPattern = new Regex(@"WaveTrack\s+(\w+)\s*\{([\s\S]*?)\}");
                var matches = waveTrackPattern.Matches(fileContent);
                var waveTrackEntries = new List<string>();

                foreach (Match match in matches)
                {
                    string trackName = match.Groups[1].Value;
                    int index = trackName.LastIndexOf("_01");
                    string trimmedTrackName = trackName.Substring(0, index);
                    waveTrackEntries.Add(trimmedTrackName);
                }

                foreach (string waveTrack in waveTrackEntries)
                {
                    Console.WriteLine($"WaveTrack found ({waveTrack})");

                    // Speaker input
                    Console.WriteLine("Input Speaker: ");
                    string speaker = Console.ReadLine();

                    // Generate XOR hashes
                    string waveHash = GenerateJenkins(waveTrack).ToString("X8");
                    string speakerHash = GenerateJenkins(speaker).ToString("X8");
                    speakerXorHashes.Add(speakerHash);

                    byte[] waveBytes = new byte[4];
                    byte[] speakerBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                    {
                        waveBytes[i] = Convert.ToByte(waveHash.Substring(i * 2, 2), 16);
                        speakerBytes[i] = Convert.ToByte(speakerHash.Substring(i * 2, 2), 16);
                    }

                    byte[] combinedBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                    {
                        combinedBytes[i] = (byte)(waveBytes[i] ^ speakerBytes[i]);
                    }
                    string combinedHash = BitConverter.ToString(combinedBytes).Replace("-", "");
                    combinedXorHashes.Add(combinedHash.ToLower());
                    Console.WriteLine($"Combined XOR hash: {combinedHash} ({speaker} & {waveTrack})");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing the file {filePath}: {ex.Message}");
            }
        }

        XDocument xml = new XDocument(
            new XElement(new XElement("Dat4", new XElement("Version", new XAttribute("value", "46158765")), new XElement("ContainerPaths"), new XElement("Items")
        )));

        for (int i = 0; i < args.Length; i++) // Assign the container paths
        {
           string containerPath = dlcDeviceName.ToUpper() + "\\" + Path.GetFileNameWithoutExtension(args[i]).ToUpper();
           xml.Element("Dat4").Element("ContainerPaths").Add(new XElement("Item", containerPath));
        }

        for (int i = 0; i < combinedXorHashes.Count; i++) // Assign the WAV data
        {
            xml.Element("Dat4").Element("Items").Add(new XElement("Item", new XAttribute("type", "ByteArray"), new XElement("Name", "hash_" + combinedXorHashes[i].ToLower()), new XElement("RawData", "01 00 00")));
        }

        for (int i = 0; i < speakerXorHashes.Count; i++) // Now assign the speakers
        {
            xml.Element("Dat4").Element("Items").Add(new XElement("Item", new XAttribute("type", "ByteArray"), new XElement("Name", "hash_" + speakerXorHashes[i].ToLower()), new XElement("RawData", "00")));
        }

        for (int i = 0; i < args.Length; i++) // And finally put the audio container linkers
        {
            string containerHash = dlcDeviceName.ToLower() + "\\" + Path.GetFileNameWithoutExtension(args[i]).ToLower();
            xml.Element("Dat4").Element("Items").Add(new XElement("Item", new XAttribute("type", "Container"), new XAttribute("ntOffset", 0 /* Debugging purposes */), new XElement("Name", i), new XElement("ContainerHash", containerHash)));
        }

        xml.Save($"{dlcDeviceName}_speech.dat4.rel.xml");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Successfully generated!");
        Thread.Sleep(10000);
    }
    public static uint GenerateJenkins(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        uint hash = 0;

        foreach (char c in input)
        {
            hash += c;
            hash += (hash << 10);
            hash ^= (hash >> 6);
        }

        hash += (hash << 3);
        hash ^= (hash >> 11);
        hash += (hash << 15);

        return hash;
    }
}