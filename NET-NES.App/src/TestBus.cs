public class TestBus : IBus {
    public byte[] ram; //64 KB RAM

    public TestBus() {
        ram = new byte[65536];

        Console.WriteLine("Test Bus init");
    }

    public void Write(ushort address, byte value) {
        ram[address] = value;
    }
    public byte Read(ushort address) {
        return ram[address];
    }
}