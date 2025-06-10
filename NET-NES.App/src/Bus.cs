public class Bus : IBus{
    public CPU cpu;
    public PPU ppu;
    public Cartridge cartridge;

    public byte[] ram; //2KB RAM

    public Input input = new Input();

    public Bus(Cartridge cartridge) {
        this.cartridge = cartridge;
        cpu = new CPU(this);
        ppu = new PPU(this);

        ram = new byte[2048];

        Console.WriteLine("Bus init");
    }

    public byte Read(ushort address) {
        if (address == 0x4016) {
            return input.Read4016(); //NES controller input
        }

        if (address >= 0x2000 && address <= 0x3FFF) {
            ushort reg = (ushort)(0x2000 + (address & 0x0007));
            byte result = ppu.ReadPPURegister(reg);
            return result;
        } else if (address >= 0x0000 && address < 0x2000) {
            return ram[address & 0x07FF];
        } else if (address >= 0x6000 && address <= 0xFFFF) {
            return cartridge.CPURead(address);
        }

        return 0;
    }

    public void Write(ushort address, byte value) {
        if (address == 0x4016) {
            input.Write4016(value);
            return;
        }

        if (address == 0x4014) {
            ppu.WriteOAMDMA(value);
            return;
        }

        if (address >= 0x2000 && address <= 0x3FFF) {
            ushort reg = (ushort)(0x2000 + (address & 0x0007));
            ppu.WritePPURegister(reg, value);
        } else if (address >= 0x0000 && address < 0x2000) {
            ram[address & 0x07FF] = value;
        } else if (address >= 0x6000 && address <= 0xFFFF) {
            cartridge.CPUWrite(address, value);
        }
    }
}