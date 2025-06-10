using Newtonsoft.Json;

class JSONTest {
    public class ProcessorState {
        public int pc { get; set; }
        public int s { get; set; }
        public int a { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int p { get; set; }

        public List<List<int>> ram { get; set; } = new List<List<int>>();
    }
    public class Test {
        public string name { get; set; } = "";
        public ProcessorState initial { get; set; } = new ProcessorState();
        public ProcessorState final { get; set; } = new ProcessorState();
        public List<List<object>> cycles { get; set; } = new List<List<object>>();
    }

    public IBus bus;
    public CPU cpu;

    public JSONTest() {
        bus = new TestBus();
        cpu = new CPU(bus);
    }

    public void Run(string jsonPath) {
        string filePath = jsonPath;
        
        var json = File.ReadAllText(filePath);
        var tests = JsonConvert.DeserializeObject<List<Test>>(json) ?? new List<Test>();
        foreach (var test in tests) {
            Console.WriteLine(test.name);

            cpu.PC = (ushort)test.initial.pc;
            cpu.SP = (ushort)test.initial.s;
            cpu.A = (byte)test.initial.a;
            cpu.X = (byte)test.initial.x;
            cpu.Y = (byte)test.initial.y;
            cpu.status = (byte)test.initial.p;

            string initCPU16Reg = $"PC: {cpu.PC}, SP: {cpu.SP}";
            string initCPUReg = $"A: {cpu.A}, X: {cpu.X}, Y: {cpu.Y}, P: {cpu.status}";
            string initRAM = "";

            foreach (var entry in test.initial.ram) {
                bus.Write((ushort)entry[0], (byte)entry[1]);
                initRAM += $"Address: {entry[0]}, Value: {entry[1]}\n";
            }

            int actualCycleCount = cpu.ExecuteInstruction();

            string finalCPU16Reg = $"PC: {cpu.PC}, SP: {cpu.SP}";
            string finalCPUReg = $"A: {cpu.A}, X: {cpu.X}, Y: {cpu.Y}, P: {cpu.status}";
            string finalRAM = "";

            bool isMismatch = false;
            if (cpu.A != test.final.a) { Console.WriteLine($"Mismatch in A: Expected {test.final.a}, Found {cpu.A}"); isMismatch = true; }
            if (cpu.X != test.final.x) { Console.WriteLine($"Mismatch in X: Expected {test.final.x}, Found {cpu.X}"); isMismatch = true; }
            if (cpu.Y != test.final.y) { Console.WriteLine($"Mismatch in Y: Expected {test.final.y}, Found {cpu.Y}"); isMismatch = true; }
            if (cpu.status != test.final.p) { Console.WriteLine($"Mismatch in P: Expected {test.final.p}, Found {cpu.status}"); isMismatch = true; }
            if (cpu.PC != test.final.pc) { Console.WriteLine($"Mismatch in Pc: Expected {test.final.pc}, Found {cpu.PC}"); isMismatch = true; }
            if (cpu.SP != test.final.s) { Console.WriteLine($"Mismatch in Sp: Expected {test.final.s}, Found {cpu.SP}"); isMismatch = true; }
            
            foreach (var entry in test.final.ram) {
                int valueInMMU = bus.Read((ushort)entry[0]);
                finalRAM += $"Address: {entry[0]}, Value: {entry[1]}\n";

                if (valueInMMU != entry[1]) {
                    Console.WriteLine($"Mismatch in RAM at Address {entry[0]}: Expected {entry[1]}, Found {valueInMMU}");
                    isMismatch = true;
                }
            }

            int expectedCycleCount = test.cycles.Count;

            if (expectedCycleCount != actualCycleCount) {
                Console.WriteLine($"Mismatch in cycles: Expected {expectedCycleCount}, Found {actualCycleCount}");
                isMismatch = true;
            }

            if (isMismatch) {
                //To compare init and final values to JSON for full detail if init properly or anyother
                Console.WriteLine("\nCPU and RAM init:");
                Console.WriteLine(initCPU16Reg);
                Console.WriteLine(initCPUReg);
                Console.WriteLine(initRAM);

                Console.WriteLine("CPU and RAM final:");
                Console.WriteLine(finalCPU16Reg);
                Console.WriteLine(finalCPUReg);
                Console.WriteLine(finalRAM);
                
                Console.WriteLine("JSON Test:");
                string testJson = JsonConvert.SerializeObject(test, Formatting.Indented);
                Console.WriteLine(testJson);

                Environment.Exit(1);
            }
        }

        Console.WriteLine("All tests passed!");
    }
}