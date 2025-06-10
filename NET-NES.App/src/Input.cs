using Raylib_cs;

public class Input {
    public byte controllerState = 0;
    private byte controllerShift = 0;

    public void UpdateController() {
        controllerState = 0;
        if (Raylib.IsKeyDown(KeyboardKey.X)) controllerState |= 1 << 0; // A
        if (Raylib.IsKeyDown(KeyboardKey.Z)) controllerState |= 1 << 1; // B
        if (Raylib.IsKeyDown(KeyboardKey.RightShift) || Raylib.IsKeyDown(KeyboardKey.LeftShift)) controllerState |= 1 << 2; // Select
        if (Raylib.IsKeyDown(KeyboardKey.Enter)) controllerState |= 1 << 3; // Start
        if (Raylib.IsKeyDown(KeyboardKey.Up)) controllerState |= 1 << 4; // Up
        if (Raylib.IsKeyDown(KeyboardKey.Down)) controllerState |= 1 << 5; // Down
        if (Raylib.IsKeyDown(KeyboardKey.Left)) controllerState |= 1 << 6; // Left
        if (Raylib.IsKeyDown(KeyboardKey.Right)) controllerState |= 1 << 7; // Right
    }

    public void Write4016(byte value) {
        if ((value & 1) != 0) {
            controllerShift = controllerState;
        }
    }

    public byte Read4016() {
        byte result = (byte)(controllerShift & 1);
        controllerShift >>= 1;
        return result;
    }
}