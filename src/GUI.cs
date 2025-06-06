#pragma warning disable

using ImGuiNET;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Raylib_cs;
using rlImGui_cs;
using System.Threading.Tasks;


public class GUI
{
    private NES nes;

    private FileDialog fileDialog;
    private string selectedFilePath = "";

    private bool showScaleWindow = false;
    private bool showAboutWindow = false;
    private bool showManualWindow = false;

    private volatile bool isAnalyzingFrame = false; // Add this field

    Image icon;
    Texture2D backgroundTexture;

    public GUI()
    {
        if (!Helper.raylibLog) Raylib.SetTraceLogLevel(TraceLogLevel.None);

        Raylib.InitWindow(256 * Helper.scale, 240 * Helper.scale, "NES");
        Raylib.SetTargetFPS(60);

        rlImGui.Setup(true);

        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        //Unsafe access for no imgui.ini files
        var io = ImGui.GetIO();
        unsafe
        {
            IntPtr ioPtr = (IntPtr)io.NativePtr;
            ImGuiIO* imguiIO = (ImGuiIO*)ioPtr.ToPointer();
            imguiIO->IniFilename = null;
        }

        fileDialog = new FileDialog(Directory.GetCurrentDirectory());

        icon = Raylib.LoadImage(Path.Combine(AppContext.BaseDirectory, "res", "Logo.png"));
        backgroundTexture = Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory, "res", "Background.png"));

        Raylib.SetWindowIcon(icon);
    }

    public async Task RunAsync()
    {
        // ollama
        var uri = new Uri("http://localhost:11434");
        var ollama = new OllamaApiClient(uri);
        ollama.SelectedModel = "qwen2.5vl";
        // ollama.SelectedModel = "llama3.2-vision";        
        var chat = new Chat(ollama);

        var prompt = @"Act as a game player, with high expertise playing Ms Pacman.
Your job is to analyze game frames, and define the next step to be taken to win the game.
The game is Ms Pacman, so the only possible actions are: up, down, left, right or undefined.
If there is no available action return undefined.

The output should be a JSON with 2 fields: 'nextaction' and 'explanation'.

These are 3 sample JSON output :
'{ ""nextaction"": ""right"",\r\n  ""explanation"": ""Moving right will allow Ms Pacman to collect more pellets while avoiding nearby ghosts.""'
'{ ""nextaction"": ""left"",\r\n  ""explanation"": ""Moving left will help Ms Pacman avoid an approaching ghost.""'
'{ ""nextaction"": ""up"",\r\n  ""explanation"": ""Moving up will open up routes to collect more pellets.""'

Do not include ```json at the beginning of the result, ``` at the end, only return the JSON.";        

        byte controllerState = 0;
        string frameFileName = string.Empty;

        while (!Raylib.WindowShouldClose())
        {

            Raylib.SetWindowSize(256 * Helper.scale, 240 * Helper.scale);
            Raylib.BeginDrawing();
            rlImGui.Begin();

            Raylib.ClearBackground(Color.Black);


            MenuBar();

            if (Helper.romPath.Length != 0 && Helper.insertingRom == false)
            {
                // original
                // frameFileName = nes.Run();

                if (controllerState != 0)
                {
                    frameFileName = nes.Run(controllerState, true);
                    controllerState = 0;
                }
                else
                {
                    frameFileName = nes.Run();
                }

            }
            else if (Helper.insertingRom == true)
            {
                nes = new NES();
                nes.chat = chat;
                Helper.insertingRom = false;
            }
            else
            {
                Raylib.ClearBackground(Color.DarkGray);
                Raylib.DrawTextureEx(backgroundTexture, new System.Numerics.Vector2(0, -5), 0, (float)(Helper.scale * 0.50), Color.White);
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Space)) Helper.showMenuBar = !Helper.showMenuBar;

            if (Helper.fpsEnable) Raylib.DrawFPS(0, Helper.showMenuBar ? 19 : 0);

            rlImGui.End();
            Raylib.EndDrawing();

            // Only start analysis if not already running
            if (File.Exists(frameFileName) && Helper.insertingRom == false && !isAnalyzingFrame)
            {
                isAnalyzingFrame = true;

                _ = Task.Run(async () =>
                {
                    // validate that frameFileName is not an empty string
                    if (File.Exists(frameFileName))
                    {
                        try
                        {
                            byte[] imageBytes = File.ReadAllBytes(frameFileName);
                            var imageBytesEnumerable = new List<IEnumerable<byte>> { imageBytes };
                            var llmResponse = string.Empty;

                            await foreach (var answerToken in chat.SendAsync(message: prompt, imagesAsBytes: imageBytesEnumerable))
                            {
                                // show the answerToken in the Console
                                llmResponse += answerToken;
                            }

                            llmResponse = CleanLlmJsonResponse(llmResponse);
                            Console.WriteLine(llmResponse);

                            // desearialize the llmResponse json string into a GameActionResult
                            GameActionResult gar =
                            System.Text.Json.JsonSerializer.Deserialize<GameActionResult>(llmResponse);    


                            // display the next action in the console, with the current time with milliseconds as prefix
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Next Action: {gar.nextaction}");
                            Console.WriteLine($"\t{gar.explanation}");

                            // Send the corresponding key to the keyboard for the detected action
                            switch (gar.nextaction)
                            {
                                case "up":
                                    controllerState |= 1 << 4; // Up
                                    break;
                                case "down":
                                    controllerState |= 1 << 5; // Down
                                    break;
                                case "left":
                                    controllerState |= 1 << 6; // Left
                                    break;
                                case "right":
                                    controllerState |= 1 << 7; // Right
                                    break;
                                case "undefined":
                                    controllerState = 0; // No action
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error during frame analysis: {ex.Message}");
                        }
                        finally
                        {
                            isAnalyzingFrame = false;
                        }
                    }
                });
            }
        }

        Raylib.CloseWindow();
    }

    private static string CleanLlmJsonResponse(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return llmResponse;

        var lines = llmResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Find the first line that starts with '{' and the last line that ends with '}'
        int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("{"));
        int end = Array.FindLastIndex(lines, l => l.TrimEnd().EndsWith("}"));

        if (start >= 0 && end >= start)
        {
            var jsonLines = lines[start..(end + 1)];
            return string.Join("\n", jsonLines);
        }

        // If not found, return the original (may throw on deserialization)
        return llmResponse.Trim();
    }

    public void MenuBar()
    {
        if (Helper.showMenuBar)
        {
            if (ImGui.BeginMainMenuBar())
            {
                ImGui.Text("NET-NES");

                ImGui.Separator();

                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open ROM"))
                    {
                        fileDialog.Open();
                        Helper.showMenuBar = false;
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Config"))
                {
                    if (ImGui.MenuItem("Window Size"))
                    {
                        showScaleWindow = true;
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Debug"))
                {
                    if (ImGui.MenuItem("FPS Enable", null, Helper.fpsEnable))
                    {
                        Helper.fpsEnable = !Helper.fpsEnable;
                    }
                    if (ImGui.MenuItem("Sprite0 Hit Disable", null, Helper.debugs0h))
                    {
                        Helper.debugs0h = !Helper.debugs0h;
                        Console.WriteLine("Sprite0 Hit Check Disable: " + Helper.debugs0h);
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Help"))
                {
                    if (ImGui.MenuItem("Manual"))
                    {
                        showManualWindow = true;
                    }
                    if (ImGui.MenuItem("About"))
                    {
                        showAboutWindow = true;
                    }
                    ImGui.EndMenu();
                }
            }
        }
        else
        {
            Helper.showMenuBar = ImGui.GetMousePos().Y <= 20.0f && ImGui.GetMousePos().Y != 0;
        }

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 0), ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(256 * Helper.scale, 240 * Helper.scale), ImGuiCond.Appearing);
        if (fileDialog.Show(ref selectedFilePath))
        {
            Helper.romPath = selectedFilePath;
            Helper.insertingRom = true;
        }

        ScaleWindow();
        ManualWindow();
        AboutWindow();
    }

    public void ScaleWindow()
    {
        if (showScaleWindow)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 20), ImGuiCond.Appearing);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(125 * 2, 50 * 2), ImGuiCond.Appearing);
            if (ImGui.Begin("Window Size Config", ref showScaleWindow))
            {
                ImGui.SliderInt("Scale", ref Helper.scale, 1, 10);

                ImGui.Spacing();

                if (ImGui.Button("Close"))
                {
                    showScaleWindow = false;
                }
            }
            ImGui.End();
        }
    }

    public void AboutWindow()
    {
        if (showAboutWindow)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 20), ImGuiCond.Appearing);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(125 * 2, 120 * 2), ImGuiCond.Appearing);
            if (ImGui.Begin("About", ref showAboutWindow))
            {
                ImGui.Text("NET-NES");
                ImGui.Text(Helper.version.ToString());
                ImGui.Text("Made by Bot Randomness :)");

                ImGui.Text(" ____________________________ ");
                ImGui.Text("| |  NES               |---| |");
                ImGui.Text("| |____________________|___| |");
                ImGui.Text("|____________________________|");
                ImGui.Text("|                     1  2   |");
                ImGui.Text(" \\ O [ ] [ ]          D  D  / ");
                ImGui.Text("  *------------------------*  ");

                if (ImGui.Button("Close"))
                {
                    showAboutWindow = false;
                }
            }
            ImGui.End();
        }
    }

    public void ManualWindow()
    {
        if (showManualWindow)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 20), ImGuiCond.Appearing);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(125 * 2, 120 * 2), ImGuiCond.Appearing);
            if (ImGui.Begin("Manual", ref showManualWindow))
            {
                ImGui.Text("Controls: (A)=X, (B)=Z, \nD-Pad=ArrowKeys, [START]=ENTER, \n[SELECT]=SHIFT");
                ImGui.Spacing();
                ImGui.Text("Press [SPACE] to toggle the Menu \nBar.");
                ImGui.Spacing();
                ImGui.Text("In Debug: Toggle Sprite0 Hit Check. \nTry this out if a game freezes");
                ImGui.Spacing();
                ImGui.Text("Only Catridge Mapper ID Supported: \n0, 1, 2, 4");

                if (ImGui.Button("Close"))
                {
                    showManualWindow = false;
                }
            }
            ImGui.End();
        }
    }

    class FileDialog
    {
        /*
        *   File Dialog for ImGui.NET
        *   This is a simple file dialog, it's flexible and expandable
        *   This can also easily be ported to other languages for other bindings or the original Dear ImGui
        *
        *   Basic Usage:
        *   FileDialog fileDialog = new FileDialog();
        *
        *   string selectedFilePath = "";
        *
        *   fileDialog.Open(); //Trigger the file dialog to open
        *
        *   if (fileDialog.Show(ref selectedFilePath)) {
        *       //Do something with string path
        *   }
        *   
        *   Made by Bot Randomness
        */

        private string currentDirectory;
        private string selectedFilePath = "";
        private bool isOpen = false;

        private bool canCancel = true;

        public FileDialog(string startDirectory, bool canCancel = true)
        {
            currentDirectory = Directory.Exists(startDirectory) ? startDirectory : Directory.GetCurrentDirectory();
            this.canCancel = canCancel;
        }

        public bool Show(ref string resultFilePath)
        {
            if (!isOpen) return false;

            bool fileSelected = false;

            if (ImGui.Begin("File Dialog", ImGuiWindowFlags.HorizontalScrollbar))
            {
                ImGui.Text("Select a file:");

                string[] directories = Directory.GetDirectories(currentDirectory);
                string[] files = Directory.GetFiles(currentDirectory, "*.*");

                ImGui.InputText("File Path", ref selectedFilePath, 260);

                if (ImGui.Button("Select File"))
                {
                    if (File.Exists(selectedFilePath))
                    {
                        resultFilePath = selectedFilePath;
                        fileSelected = true;
                        isOpen = false;
                    }
                }

                ImGui.SameLine();

                if (canCancel)
                {
                    if (ImGui.Button("Cancel"))
                    {
                        isOpen = false;
                    }
                }

                if (Path.GetPathRoot(currentDirectory) != currentDirectory)
                {
                    if (ImGui.Button("Back"))
                    {
                        currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory;
                    }
                }

                foreach (var dir in directories)
                {
                    if (ImGui.Selectable("[DIR] " + Path.GetFileName(dir)))
                    {
                        currentDirectory = dir;
                    }
                }

                foreach (var file in files)
                {
                    if (ImGui.Selectable(Path.GetFileName(file)))
                    {
                        selectedFilePath = file;
                    }
                }

                if (directories.Length == 0 && files.Length == 0)
                {
                    ImGui.Text("No files or folders found.");
                }
            }
            ImGui.End();

            return fileSelected;
        }

        public void Open()
        {
            isOpen = true;
        }
    }
}

#pragma warning restore