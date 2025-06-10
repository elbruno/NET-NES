public class NES
{
    Cartridge cartridge;
    Bus bus;

    public NES()
    {
        cartridge = new Cartridge(Helper.romPath);
        bus = new Bus(cartridge);

        bus.cpu.Reset();

        Console.WriteLine("NES");
    }

    public string Run(byte controllerState = 0, bool updateControllerState = false)
    {
        int cycles = 0;

        if (updateControllerState)
        {
            bus.input.controllerState = controllerState;
        }
        else
        {
            bus.input.UpdateController();
        }

        while (cycles < 29828)
        {
            int used = bus.cpu.ExecuteInstruction();
            cycles += used;
            bus.ppu.Step(used * 3);
        }

        var frameImageFileName = bus.ppu.DrawFrameAndSave(Helper.scale, true);

        // Task.Delay(250); // delay 500 milliseconds

        return frameImageFileName;
    }
}