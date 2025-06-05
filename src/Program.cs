public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("NET-NES");

        Helper.Flags(args);

        if (Helper.mode == 1)
        {
            GUI gui = new GUI();

            await gui.RunAsync();
        }
        else if (Helper.mode == 2)
        {
            TestRunner testRunner = new TestRunner();

            testRunner.Run(Helper.jsonPath);
        }
    }
}