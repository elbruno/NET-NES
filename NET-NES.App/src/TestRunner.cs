public class TestRunner {

    public TestRunner() {

    }

    public void Run(string test) {
        if (test != "all") {
            if (!File.Exists(Path.Combine("test", "v1", test))) {
                Console.WriteLine("Test file \"" + Path.Combine("test", "v1", test) + "\" does not exist");
                Console.WriteLine("Provide JSON test file, or to test all, pass in \"all\"");
                Environment.Exit(1);
            }
            JSONTest jsonTest = new JSONTest();
            jsonTest.Run(Path.Combine("test", "v1", test));
        } else {
            if (!Directory.Exists(Path.Combine("test", "v1"))) {
                Console.WriteLine("Could not find directory \"" + Path.Combine("test", "v1") + "\"");
                Console.WriteLine("Provide JSON test file, or to test all, pass in \"all\"");
                Environment.Exit(1);
            }

            string[] testFiles = Directory.GetFiles(Path.Combine("test", "v1"));

            string[] skipArray = {
                "80.json", "89.json", "9e.json",
                "02.json", "03.json", "04.json", "07.json", "xx.json", "0b.json", "0c.json", "0f.json",
                "12.json", "13.json", "14.json", "17.json", "1a.json", "1b.json", "1c.json", "1f.json",
                "22.json", "23.json", "xx.json", "27.json", "xx.json", "2b.json", "xx.json", "2f.json",
                "32.json", "33.json", "34.json", "37.json", "3a.json", "3b.json", "3c.json", "3f.json",
                "42.json", "43.json", "44.json", "47.json", "xx.json", "4b.json", "xx.json", "4f.json",
                "52.json", "53.json", "54.json", "57.json", "5a.json", "5b.json", "5c.json", "5f.json",
                "62.json", "63.json", "64.json", "67.json", "xx.json", "6b.json", "xx.json", "6f.json",
                "72.json", "73.json", "74.json", "77.json", "7a.json", "7b.json", "7c.json", "7f.json",
                "82.json", "83.json", "xx.json", "87.json", "xx.json", "8b.json", "xx.json", "8f.json",
                "92.json", "93.json", "xx.json", "97.json", "xx.json", "9b.json", "9c.json", "9f.json",
                "xx.json", "a3.json", "xx.json", "a7.json", "xx.json", "ab.json", "xx.json", "af.json",
                "b2.json", "b3.json", "xx.json", "b7.json", "xx.json", "bb.json", "xx.json", "bf.json",
                "c2.json", "c3.json", "xx.json", "c7.json", "xx.json", "cb.json", "xx.json", "cf.json",
                "d2.json", "d3.json", "d4.json", "d7.json", "da.json", "db.json", "dc.json", "df.json",
                "e2.json", "e3.json", "xx.json", "e7.json", "xx.json", "eb.json", "xx.json", "ef.json",
                "f2.json", "f3.json", "f4.json", "f7.json", "fa.json", "fb.json", "fc.json", "ff.json"
            };

            using StreamWriter log = new StreamWriter("log.txt", append: false) {AutoFlush = true};
            int tested = 0;
            int skipped = 0;

            JSONTest jsonTest = new JSONTest();

            foreach (string filePath in testFiles) {
                if (skipArray.Contains(Path.GetFileName(filePath), StringComparer.OrdinalIgnoreCase)) {
                    Console.WriteLine($"Skipping test: {Path.GetFileName(filePath)}");
                    log.WriteLine($"Skipping test: {Path.GetFileName(filePath)}");
                    skipped += 1;
                    continue;
                }

                Console.WriteLine($"Running test: {Path.GetFileName(filePath)}");
                log.WriteLine($"Running test: {Path.GetFileName(filePath)}");
                tested += 1;
                jsonTest.Run(filePath);
            }

            Console.WriteLine($"Number of Tested Opcodes: {tested}, Number of Skipped Opcodes: {skipped}");
            log.WriteLine($"Number of Tested Opcodes: {tested}, Number of Skipped Opcodes: {skipped}");
        }
    }
}