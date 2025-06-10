public class Cartridge {
    public byte[] rom;

    public byte[] prgROM;
    public byte[] chrROM;

    public int prgBanks;
    public int chrBanks;
    public int mapperID;
    public bool mirrorHorizontal;
    public bool mirrorVertical;
    public Mirroring mirroringMode;
    public bool hasBattery;

    public byte[] prgRAM;
    public byte[] chrRAM;

    public IMapper mapper;

    public Cartridge(string romPath) {
        rom = File.ReadAllBytes(romPath);

        if (rom[0] != 'N' || rom[1] != 'E' || rom[2] != 'S' || rom[3] != 0x1A) {
            Console.WriteLine("Invalid iNES Header!");
            Environment.Exit(1);
        }

        prgBanks = rom[4];
        chrBanks = rom[5];

        byte flag6 = rom[6];
        byte flag7 = rom[7];

        mirrorVertical = (flag6 & 0x01) != 0;
        mirrorHorizontal = !mirrorVertical;
        hasBattery = (flag6 & 0x02) != 0;

        if ((flag6 & 0x08) != 0) {
            
        } else if ((flag6 & 0x01) != 0) {
            mirroringMode = Mirroring.Vertical;
        } else {
            mirroringMode = Mirroring.Horizontal;
        }

        mapperID = flag6 >> 4 | ((flag7 >> 4) << 4);

        int prgSize = prgBanks * 16 * 1024;
        int chrSize = chrBanks * 8 * 1024;

        int offset = 16; //iNES rom is 16 bytes
        prgROM = new byte[prgSize];
        Array.Copy(rom, offset, prgROM, 0, prgSize);

        offset += prgSize;
        chrROM = new byte[chrSize];
        Array.Copy(rom, offset, chrROM, 0, chrSize);

        prgRAM = new byte[8 * 1024];
        chrRAM = new byte[8 * 1024];

        switch (mapperID) {
            case 0:
                mapper = new Mapper0(this);
                break;
            case 1:
                mapper = new Mapper1(this);
                break;
            case 2:
                mapper = new Mapper2(this);
                break;
            case 4:
                mapper = new Mapper4(this);
                break;
            default:
                Console.WriteLine("Mapper " + mapperID + " is not supported");
                Environment.Exit(1);
                break;
        }
        mapper.Reset();

        //Console.WriteLine($"Cartridge loaded: Mapper {mapperID}, PRG {prgBanks * 16}KB, CHR {(chrSize > 0 ? chrBanks * 8 : 8)}KB");
        Console.WriteLine($"Cartridge loaded: Mapper {mapperID}, PRG-ROM {prgBanks * 16}KB, {(chrSize > 0 ? $"{chrBanks * 8}KB CHR-ROM" : "CHR-RAM")}");
    }

    public byte CPURead(ushort address) {
        return mapper.CPURead(address);
    }

    public void CPUWrite(ushort address, byte value) {
        mapper.CPUWrite(address, value);
    }

    public byte PPURead(ushort address) {
        return mapper.PPURead(address);
    } 
    public void PPUWrite(ushort address, byte value) {
        mapper.PPUWrite(address, value);
    }

    public void SetMirroring(Mirroring mode) {
        mirroringMode = mode;
        mirrorVertical = mode == Mirroring.Vertical;
        mirrorHorizontal = mode == Mirroring.Horizontal;
    }
}

public enum Mirroring {
    Horizontal,
    Vertical,
    SingleScreenA,
    SingleScreenB
}