using Raylib_cs;

public class PPU {
    private Bus bus;

    private byte[] vram; //2KB VRAM
    private byte[] paletteRAM; //32 bytes Palette RAM
    private byte[] oam; //256 bytes OAM

    private const int ScreenWidth = 256;
    private const int ScreenHeight = 240;
    private const int CyclesPerScanlines = 341;
    private const int TotalScanlines = 262;

    private byte PPUCTRL; //$2000
    private byte PPUMASK; //$2001
    private byte PPUSTATUS; //$2002
    private byte OAMADDR; //$2003
    private byte OAMDATA; //$2004
    private byte PPUSCROLLX, PPUSCROLLY; //$2005
    private ushort PPUADDR; //$2006
    private byte PPUDATA; //$2007

    private bool addrLatch = false;
    private byte ppuDataBuffer;

    private byte fineX; //x
    private bool scrollLatch; //w
    private ushort v; //current VRAM address
    private ushort t; //temp VRAM address

    private int scanlineCycle;
    private int scanline;

    private Image image;
    private Texture2D texture;
    public int textureX = 0;
    public int textureY = 0;

    private Color[] frameBuffer;
    Color[] scanlineBuffer = new Color[ScreenWidth];

    public PPU(Bus bus) {
        this.bus = bus;

        vram = new byte[2048];
        paletteRAM = new byte[32];
        oam = new byte[256];

        image = Raylib.GenImageColor(ScreenWidth, ScreenHeight, Color.Black);
        texture = Raylib.LoadTextureFromImage(image);
        frameBuffer = new Color[ScreenWidth * ScreenHeight];

        PPUADDR = 0x0000;
        PPUCTRL = 0x00;
        PPUSTATUS = 0x00;
        PPUMASK = 0x00;

        ppuDataBuffer = 0x00;

        scanlineCycle = 0;
        scanline = 0;

        Console.WriteLine("PPU init");
    }

    public void Step(int elapsedCycles) {
        for (int i = 0; i < elapsedCycles; i++) {
            if (scanline == 0 && scanlineCycle == 0) {
                PPUSTATUS &= 0x3F;
            }

            if (scanline >= 0 && scanline < 240 && scanlineCycle == 260) {
                if ((PPUMASK & 0x18) != 0 && bus.cartridge.mapper is Mapper4) {
                    Mapper4 mmc3 = (Mapper4)bus.cartridge.mapper;
                    mmc3.RunScanlineIRQ();
                    if (mmc3.IRQPending()) {
                        bus.cpu.RequestIRQ(true);
                        mmc3.ClearIRQ();
                    }
                }
            }

            scanlineCycle++;

            if (scanlineCycle >= 341) {
                scanlineCycle = 0;

                if (scanline >= 0 && scanline < 240) {
                    CopyXFromTToV();
                    RenderScanline(scanline);
                    IncrementY();
                }

                if (scanline == 241) {
                    PPUSTATUS |= 0x80;
                    if ((PPUCTRL & 0x80) != 0) {
                        bus.cpu.RequestNMI();
                    }
                }

                if (scanline == 261) {
                    v = t;
                }

                scanline++;
                if (scanline == TotalScanlines) {
                    scanline = 0;
                }
            }
        }
    }

    bool[] bgMask = new bool[ScreenWidth];
    private void RenderScanline(int scanline) {
        Array.Clear(scanlineBuffer, 0, ScreenWidth);
        Array.Clear(bgMask, 0, ScreenWidth);
        
        RenderBackground(bgMask);
        RenderSprite(bgMask);

        Array.Copy(scanlineBuffer, 0, frameBuffer, scanline * ScreenWidth, ScreenWidth);
    }

    public void RenderBackground(bool[] bgMask) {
        if ((PPUMASK & 0x08) == 0) return;

        ushort renderV = v;

        for (int tile = 0; tile < 33; tile++) {
            int coarseX = renderV & 0x001F;
            int coarseY = (renderV >> 5) & 0x001F;
            int nameTable = (renderV >> 10) & 0x0003;

            int baseNTAddr = 0x2000 + (nameTable * 0x400);
            int tileAddr = baseNTAddr + (coarseY * 32) + coarseX;
            byte tileIndex = Read((ushort)tileAddr);

            int fineY = (renderV >> 12) & 0x7;
            int patternTable = (PPUCTRL & 0x10) != 0 ? 0x1000 : 0x0000;
            int patternAddr = patternTable + (tileIndex * 16) + fineY;
            byte plane0 = Read((ushort)patternAddr);
            byte plane1 = Read((ushort)(patternAddr + 8));

            int attributeX = coarseX / 4;
            int attributeY = coarseY / 4;
            int attrAddr = baseNTAddr + 0x3C0 + attributeY * 8 + attributeX;
            byte attrByte = Read((ushort)attrAddr);

            int attrShift = ((coarseY % 4) / 2) * 4 + ((coarseX % 4) / 2) * 2;
            int paletteIndex = (attrByte >> attrShift) & 0x03;

            for (int i = 0; i < 8; i++) {
                int pixel = tile * 8 + i - fineX;
                if (pixel < 0 || pixel >= ScreenWidth) continue;

                int bitIndex = 7 - i;
                int bit0 = (plane0 >> bitIndex) & 1;
                int bit1 = (plane1 >> bitIndex) & 1;
                int colorIndex = bit0 | (bit1 << 1);

                if (colorIndex != 0) bgMask[pixel] = true;

                scanlineBuffer[pixel] = GetColorFromPalette(colorIndex, paletteIndex);
            }

            IncrementX(ref renderV);
        }
    }

    public void RenderSprite(bool[] bgMask) {
        bool showSprites = (PPUMASK & 0x10) != 0;

        if (showSprites) {
            bool isSprite8x16 = (PPUCTRL & 0x20) != 0;
            
            bool[] spritePixelDrawn = new bool[ScreenWidth];

            for (int i = 0; i < 64; i++) {
                int offset = i * 4;
                byte spriteY = oam[offset];
                byte tileIndex = oam[offset + 1];
                byte attributes = oam[offset + 2];
                byte spriteX = oam[offset + 3];

                int paletteIndex = attributes & 0b11;
                bool flipX = (attributes & 0x40) != 0;
                bool flipY = (attributes & 0x80) != 0;
                bool priority  = (attributes & 0x20) == 0;

                int tileHeight = isSprite8x16 ? 16 : 8;
                if (scanline < spriteY || scanline >= spriteY + tileHeight)
                    continue;

                int subY = scanline - spriteY;
                if (flipY) subY = tileHeight - 1 - subY;

                int subTileIndex = isSprite8x16 ? (tileIndex & 0xFE) + (subY / 8) : tileIndex;
                int patternTable = isSprite8x16
                    ? ((tileIndex & 1) != 0 ? 0x1000 : 0x0000)
                    : ((PPUCTRL & 0x08) != 0 ? 0x1000 : 0x0000);
                int baseAddr = patternTable + subTileIndex * 16;

                byte plane0 = Read((ushort)(baseAddr + (subY % 8)));
                byte plane1 = Read((ushort)(baseAddr + (subY % 8) + 8));

                for (int x = 0; x < 8; x++) {
                    int bit = flipX ? x : 7 - x;
                    int bit0 = (plane0 >> bit) & 1;
                    int bit1 = (plane1 >> bit) & 1;
                    int color = bit0 | (bit1 << 1);
                    if (color == 0) continue;

                    int px = spriteX + x;
                    
                    if (px < 0 || px >= ScreenWidth) continue;

                    //Sprite 0 hit detection
                    if (i == 0 && bgMask[px] && color != 0 && Helper.debugs0h == false) {
                        PPUSTATUS |= 0x40;
                    } else if (Helper.debugs0h == true) { //Debug to skip check for Sprite0 Hit
                        PPUSTATUS |= 0x40;
                    }

                    if (spritePixelDrawn[px]) continue;
                    
                    bool shouldDraw = true;
                    if (!priority  && bgMask[px]) {
                        shouldDraw = false;
                    }

                    if (shouldDraw) {
                        scanlineBuffer[px] = GetSpriteColor(color, paletteIndex);
                        spritePixelDrawn[px] = true;
                    }
                }
            }
        }
    }

    public void WritePPURegister(ushort address, byte value) {
        switch (address) {
            case 0x2000:
                PPUCTRL = value;
                t = (ushort)((t & 0xF3FF) | ((value & 0x03) << 10));
                break;
            case 0x2001:
                PPUMASK = value;
                break;
            case 0x2002:
                PPUSTATUS &= 0x7F;
                scrollLatch = false;
                break;
            case 0x2003:
                OAMADDR = value;
                break;
            case 0x2004:
                OAMDATA = value;
                oam[OAMADDR++] = OAMDATA;
                break;
            case 0x2005:
                if (!scrollLatch) {
                    PPUSCROLLX = value;
                    fineX = (byte)(value & 0x07);
                    t = (ushort)((t & 0xFFE0) | (value >> 3));
                } else {
                    PPUSCROLLY = value;
                    t = (ushort)((t & 0x8FFF) | ((value & 0x07) << 12));
                    t = (ushort)((t & 0xFC1F) | ((value & 0xF8) << 2));
                }
                scrollLatch = !scrollLatch;
                break;
            case 0x2006:
                if (!addrLatch) {
                    t = (ushort)((value << 8) | (t & 0x00FF));
                    PPUADDR = t;
                } else {
                    t = (ushort)((t & 0xFF00) | value);
                    PPUADDR = t;
                    v = t;
                }
                addrLatch = !addrLatch;
                break;
            case 0x2007:
                PPUDATA = value;
                Write(PPUADDR, PPUDATA);
                PPUADDR += ((PPUCTRL & 0x04) != 0) ? (ushort)32 : (ushort)1;
                v = PPUADDR;
                break;
        }
    }

    public byte ReadPPURegister(ushort address) {
        byte result = 0x00;

        switch (address) {
            case 0x2000:
                result = PPUCTRL;
                break;
            case 0x2002:
                result = PPUSTATUS;
                PPUSTATUS &= 0x3F;
                addrLatch = false;
                break;
            case 0x2004:
                result = oam[OAMADDR];
                break;
            case 0x2006:
                result = (byte)(PPUADDR >> 8);
                break;
            case 0x2007:
                result = ppuDataBuffer;
                ppuDataBuffer = Read(PPUADDR);
                
                if (PPUADDR >= 0x3F00) {
                    result = ppuDataBuffer;
                }
                
                PPUADDR += ((PPUCTRL & 0x04) != 0) ? (ushort)32 : (ushort)1;
                return result;
        }
        return result;
    }

    public byte Read(ushort address) {
        address = (ushort)(address & 0x3FFF);

        if (address < 0x2000) {
            return bus.cartridge.PPURead(address);
        } else if (address >= 0x2000 && address <= 0x3EFF) {
            ushort mirrored = MirrorVRAMAddress(address);
            return vram[mirrored];
        } else if (address >= 0x3F00 && address <= 0x3FFF) {
            ushort mirrored = (ushort)(address & 0x1F);
            if (mirrored >= 0x10 && (mirrored % 4) == 0) mirrored -= 0x10;
            return paletteRAM[mirrored];
        }

        return 0;
    }

    public void Write(ushort address, byte value) {
        address = (ushort)(address & 0x3FFF);

        if (address < 0x2000) {
            bus.cartridge.PPUWrite(address, value);
        } else if (address >= 0x2000 && address <= 0x3EFF) {
            ushort mirrored = MirrorVRAMAddress(address);
            vram[mirrored] = value;
        } else if (address >= 0x3F00 && address <= 0x3FFF) {
            ushort mirrored = (ushort)(address & 0x1F);
            if (mirrored >= 0x10 && (mirrored % 4) == 0) mirrored -= 0x10;
            paletteRAM[mirrored] = value;
        }
    }

    private ushort MirrorVRAMAddress(ushort address) {
        ushort offset = (ushort)(address & 0x0FFF);

        int ntIndex = offset / 0x400;
        int innerOffset = offset % 0x400;

        switch (bus.cartridge.mirroringMode) {
            case Mirroring.Vertical:
                return (ushort)((ntIndex % 2) * 0x400 + innerOffset);
            case Mirroring.Horizontal:
                return (ushort)(((ntIndex / 2) * 0x400) + innerOffset);
            case Mirroring.SingleScreenA:
                return (ushort)(innerOffset);
            case Mirroring.SingleScreenB:
                return (ushort)(0x400 + innerOffset);
            default:
                return offset;
        }
    }

    public void WriteOAMDMA(byte page) {
        ushort baseAddr = (ushort)(page << 8);
        for (int i = 0; i < 256; i++) {
            byte value = bus.Read((ushort)(baseAddr + i));
            oam[OAMADDR++] = value;
        }
    }

    private void IncrementY() {
        if ((v & 0x7000) != 0x7000) {
            v += 0x1000;
        } else {
            v &= 0x8FFF;
            int y = (v & 0x03E0) >> 5;
            if (y == 29) {
                y = 0;
                v ^= 0x0800;
            } else if (y == 31) {
                y = 0;
            } else {
                y += 1;
            }
            v = (ushort)((v & 0xFC1F) | (y << 5));
        }
    }

    private void IncrementX(ref ushort addr) {
        if ((addr & 0x001F) == 31) {
            addr &= 0xFFE0;
            addr ^= 0x0400;
        } else {
            addr++;
        }
    }

    private void CopyXFromTToV() {
        v = (ushort)((v & 0xFBE0) | (t & 0x041F));
    }

    private Color GetSpriteColor(int colorIndex, int paletteIndex) {
        int paletteBase = 0x11 + paletteIndex * 4;
        byte paletteColor = paletteRAM[paletteBase + (colorIndex - 1)];
        return NesPalette[paletteColor % 64];
    }

    private Color GetColorFromPalette(int colorIndex, int paletteIndex) {
        if (colorIndex == 0) {
            byte bgColorIndex = paletteRAM[0];
            return NesPalette[bgColorIndex % 64];
        }

        int paletteBase = 1 + (paletteIndex * 4);
        byte paletteColorIndex = paletteRAM[(paletteBase + colorIndex - 1) % 32];
        return NesPalette[paletteColorIndex % 64];
    }    

    public void DrawFrame(int scale) {
        for (int y = 0; y < ScreenHeight; y++) {
            for (int x = 0; x < ScreenWidth; x++) {
                Color color = frameBuffer[y * ScreenWidth + x];
                Raylib.ImageDrawPixel(ref image, x, y, color);
            }
        }

        unsafe {
            Raylib.UpdateTexture(texture, image.Data);
        }

        Raylib.DrawTextureEx(texture, new System.Numerics.Vector2(textureX, textureY), 0.0f, scale, Color.White);
    }

    public string DrawFrameAndSave(int scale, bool saveImage = false)
    {
        var fileName = string.Empty;

        for (int y = 0; y < ScreenHeight; y++)
        {
            for (int x = 0; x < ScreenWidth; x++)
            {
                Color color = frameBuffer[y * ScreenWidth + x];
                Raylib.ImageDrawPixel(ref image, x, y, color);
            }
        }

        unsafe
        {
            Raylib.UpdateTexture(texture, image.Data);
        }

        Raylib.DrawTextureEx(texture, new System.Numerics.Vector2(textureX, textureY), 0.0f, scale, Color.White);

        if (saveImage)
        {
            fileName = GetImageFilename();
            unsafe
            {
                Raylib.ExportImage(image, ToSbytePtr(fileName));
            }
        }

        return fileName; // Return the filename for the saved image 
    }

    // get a new filename using the current execution path + a folder named "images" + a timestamp with .png extension
    public string GetImageFilename() {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "images");
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            string filename = $"nes_frame_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.png";
            return Path.Combine(path, filename);
    }

    // Helper to convert string to sbyte* for Raylib interop
    private static unsafe sbyte* ToSbytePtr(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str + "\0");
        var ptr = (sbyte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
        for (int i = 0; i < bytes.Length; i++) ptr[i] = (sbyte)bytes[i];
        return ptr;
    }

    //NES 64 Color Palette
    static readonly Color[] NesPalette = new Color[] {
        new Color(84, 84, 84, 255),    new Color(0, 30, 116, 255),   new Color(8, 16, 144, 255),   new Color(48, 0, 136, 255),
        new Color(68, 0, 100, 255),    new Color(92, 0, 48, 255),    new Color(84, 4, 0, 255),     new Color(60, 24, 0, 255),
        new Color(32, 42, 0, 255),     new Color(8, 58, 0, 255),     new Color(0, 64, 0, 255),     new Color(0, 60, 0, 255),
        new Color(0, 50, 60, 255),     new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255),
        new Color(152, 150, 152, 255), new Color(8, 76, 196, 255),   new Color(48, 50, 236, 255),  new Color(92, 30, 228, 255),
        new Color(136, 20, 176, 255),  new Color(160, 20, 100, 255), new Color(152, 34, 32, 255),  new Color(120, 60, 0, 255),
        new Color(84, 90, 0, 255),     new Color(40, 114, 0, 255),   new Color(8, 124, 0, 255),    new Color(0, 118, 40, 255),
        new Color(0, 102, 120, 255),   new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255),
        new Color(236, 238, 236, 255), new Color(76, 154, 236, 255), new Color(120, 124, 236, 255),new Color(176, 98, 236, 255),
        new Color(228, 84, 236, 255),  new Color(236, 88, 180, 255), new Color(236, 106, 100, 255),new Color(212, 136, 32, 255),
        new Color(160, 170, 0, 255),   new Color(116, 196, 0, 255),  new Color(76, 208, 32, 255),  new Color(56, 204, 108, 255),
        new Color(56, 180, 204, 255),  new Color(60, 60, 60, 255),   new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255),
        new Color(236, 238, 236, 255), new Color(168, 204, 236, 255),new Color(188, 188, 236, 255),new Color(212, 178, 236, 255),
        new Color(236, 174, 236, 255), new Color(236, 174, 212, 255),new Color(236, 180, 176, 255),new Color(228, 196, 144, 255),
        new Color(204, 210, 120, 255), new Color(180, 222, 120, 255),new Color(168, 226, 144, 255),new Color(152, 226, 180, 255),
        new Color(160, 214, 228, 255), new Color(160, 162, 160, 255),new Color(0, 0, 0, 255),      new Color(0, 0, 0, 255)
    };
}