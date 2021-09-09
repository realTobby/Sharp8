using SFML.Graphics;
using SFML.Window;
using Sharp8.CHIP8;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sharp8
{
    class Program
    {
        static RenderWindow _window;
        static CPUEight chip8;

        static readonly Stopwatch stopWatch = Stopwatch.StartNew();
        static readonly TimeSpan targetElapsedTime60Hz = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
        static readonly TimeSpan targetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 1000);
        static TimeSpan lastTime;

        static void Main(string[] args)
        {
            chip8 = new CPUEight(Draw, Beep);

            chip8.LoadProgram(System.IO.File.ReadAllBytes("breakout.ch8"));
            Console.WriteLine("==================");

            _window = new RenderWindow(new VideoMode((uint)chip8.GetScreenWidth(), (uint)chip8.GetScreenHeight()), "Sharp8 by github.com/realTobby");
            _window.SetVisible(true);
            _window.Closed += new EventHandler(OnClosed);
            _window.KeyPressed += new EventHandler<SFML.Window.KeyEventArgs>(SetKeyDown);
            _window.KeyReleased += new EventHandler<SFML.Window.KeyEventArgs>(SetKeyUp);
            while (_window.IsOpen)
            {
                _window.DispatchEvents();
                
                chip8.Tick();

                _window.Display();
            }

        }

        static void SetKeyDown(object sender, SFML.Window.KeyEventArgs e)
        {
            if (keyMapping.ContainsKey(e.Code))
                chip8.KeyDown(keyMapping[e.Code]);
        }

        static void SetKeyUp(object sender, SFML.Window.KeyEventArgs e)
        {
            if (keyMapping.ContainsKey(e.Code))
                chip8.KeyUp(keyMapping[e.Code]);
        }

        static void OnClosed(object sender, EventArgs e)
        {
            _window.Close();
        }

        static void Draw(bool[,] buffer)
        {
            _window.Clear(Color.Black);
            //var bits = screen.LockBits(new Rectangle(0, 0, screen.Width, screen.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            //unsafe
            //{
            //    byte* pointer = (byte*)bits.Scan0;

            //    for (var y = 0; y < screen.Height; y++)
            //    {
            //        for (var x = 0; x < screen.Width; x++)
            //        {
            //            pointer[0] = 0; // Blue
            //            pointer[1] = buffer[x, y] ? (byte)0x64 : (byte)0; // Green
            //            pointer[2] = 0; // Red
            //            pointer[3] = 255; // Alpha

            //            pointer += 4; // 4 bytes per pixel
            //        }
            //    }
            //}

            //screen.UnlockBits(bits);
            Sprite screen = new Sprite();
            Texture screenTex = new Texture((uint)chip8.GetScreenWidth(), (uint)chip8.GetScreenHeight());

            byte[] screenBuffer = new byte[64 * 32 * 4];
            int pixelPointer = 0;

            for (int y = 0; y < chip8.GetScreenHeight(); y++)
            {
                for(int x = 0; x < chip8.GetScreenWidth(); x++)
                {
                    screenBuffer[pixelPointer] = 0;
                    screenBuffer[pixelPointer + 1] = buffer[x, y] ? (byte)0x64 : (byte)0;
                    screenBuffer[pixelPointer + 2] = 0;
                    screenBuffer[pixelPointer + 3] = 255;
                    pixelPointer += 4;
                }
            }
            screenTex.Update(screenBuffer);
            screen.Texture = screenTex;
            screen.Position = new SFML.System.Vector2f(0, 0);
            _window.Draw(screen);
            screen.Draw(_window, RenderStates.Default);

            Console.WriteLine("====================================== DrawingOnScreen =======================================================");

        }

        static void Beep(int milliseconds)
        {
            Console.Beep(500, milliseconds);
        }

        static Dictionary<Keyboard.Key, byte> keyMapping = new Dictionary<Keyboard.Key, byte>
        {
            { Keyboard.Key.Num1, 0x1 },
            { Keyboard.Key.Num2, 0x2 },
            { Keyboard.Key.Num3, 0x3 },
            { Keyboard.Key.Num4, 0xC },
            { Keyboard.Key.Q, 0x4 },
            { Keyboard.Key.W, 0x5 },
            { Keyboard.Key.E, 0x6 },
            { Keyboard.Key.R, 0xD },
            { Keyboard.Key.A, 0x7 },
            { Keyboard.Key.S, 0x8 },
            { Keyboard.Key.D, 0x9 },
            { Keyboard.Key.F, 0xE },
            { Keyboard.Key.Z, 0xA },
            { Keyboard.Key.X, 0x0 },
            { Keyboard.Key.C, 0xB },
            { Keyboard.Key.V, 0xF },
        };

    }
}
