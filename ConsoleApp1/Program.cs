using System;
using WMPLib;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Numerics;
using System.Xml.Linq;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        DisplayBanner();
        DisplayInstructions();

        Exception? exception = null;
        string playerName = GetPlayerName();
        CenterTextHorizontally($"Welcome, {playerName}!");

        int speedInput;   // Variable to store the user's speed input
        string prompt = @"Select speed [1], [2] (default), or [3]: "; // Prompt message
        string? input; //Variable to hold user input as a string

        // Change text color for the prompt
        Console.ForegroundColor = ConsoleColor.Green;

        // Calculate the starting position to center the prompt horizontally
        int consoleWidth = Console.WindowWidth; // Get the width of the console window
        int startPosition = (consoleWidth - prompt.Length) / 2; // Center alignment calculation

        // Print the prompt at the calculated centered position
        Console.SetCursorPosition(startPosition, Console.CursorTop); // Move the cursor to the starting position
        Console.Write(prompt); // Display the prompt message

        // Position the cursor immediately after the prompt for user input
        Console.SetCursorPosition(startPosition + prompt.Length, Console.CursorTop);

        // Read and validate user input
        while (!int.TryParse(input = Console.ReadLine(), out speedInput) || speedInput < 1 || speedInput > 3)
        {
            if (string.IsNullOrWhiteSpace(input)) // If the user presses Enter without typing anything
            {
                speedInput = 2; // Default speed is 2
                break; // Exit the loop
            }
            else
            {
                // Display an error message for invalid input
                string error = "Invalid Input. Try Again...";
                Console.SetCursorPosition((consoleWidth - error.Length) / 2, Console.CursorTop); // Center the error message
                Console.WriteLine(error); // Show the error message

                // Reprint the prompt after the error message, maintaining alignment
                Console.SetCursorPosition(startPosition, Console.CursorTop); // Move cursor back to the prompt position
                Console.Write(prompt); // Display the prompt again

                // Position the cursor after the prompt for the next user input
                Console.SetCursorPosition(startPosition + prompt.Length, Console.CursorTop);
            }
        }

        // Reset color to default after the prompt
        Console.ResetColor();

        int[] velocities = { 100, 70, 50 };
        int velocity = velocities[speedInput - 1];
        char[] DirectionChars = { '■', '■', '■', '■' };
        char[] listOfFoodChars = { '◉', '◆', '●', '◈' };
        TimeSpan sleep = TimeSpan.FromMilliseconds(velocity);
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        int headerHeight = 1;
        int footerHeight = 1;
        int sideWidth = 1;

        Tile[,] map = new Tile[width, height];
        Direction? direction = null;
        Queue<(int X, int Y)> snake = new();
        (int X, int Y) = (2, 1);
        bool closeRequested = false;
        bool isPaused = false;
        Random random = new Random();

        (int specialX, int specialY) = (-1, -1); //special food variable
        DateTime specialFoodSpawnTime = DateTime.MinValue;
        TimeSpan specialFoodLifetime = TimeSpan.FromSeconds(7);
        bool specialFoodActive = false;
        int normalFoodCounter = 0; //count the number of times normal food is eaten
        bool specialFoodBlinking = false; //to control the blinking effect

        try
        {
            Console.CursorVisible = false;
            Console.Clear();
            DrawConsole(0, velocity);
            snake.Enqueue((X, Y));
            map[X, Y] = Tile.Snake;
            PositionFood();
            Console.SetCursorPosition(X, Y);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("▶︎");
            Console.ResetColor();

            //add blinking feature while waiting for the player to press a move key
            WaitForInputAndBlink();

            while (!closeRequested)
            {
                if (Console.WindowWidth != width || Console.WindowHeight != height)
                {
                    Console.Clear();
                    Console.Write("Console was resized. Snake game has ended.");
                    return;
                }
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter)
                    {
                        isPaused = !isPaused;  // Toggle pause state
                    }
                    else
                    {
                        // Update direction based on key press
                        switch (key)
                        {
                            case ConsoleKey.UpArrow:
                                if (direction != Direction.Down) direction = Direction.Up; // Prevent reversing
                                break;
                            case ConsoleKey.DownArrow:
                                if (direction != Direction.Up) direction = Direction.Down; // Prevent reversing
                                break;
                            case ConsoleKey.LeftArrow:
                                if (direction != Direction.Right) direction = Direction.Left; // Prevent reversing
                                break;
                            case ConsoleKey.RightArrow:
                                if (direction != Direction.Left) direction = Direction.Right; // Prevent reversing
                                break;
                            case ConsoleKey.Escape:
                                closeRequested = true; // Allow the player to exit the game
                                break;
                        }
                    }
                }
                if (!isPaused)
                {

                    //handle snake movement
                    switch (direction)
                    {
                        case Direction.Up: Y--; break;
                        case Direction.Down: Y++; break;
                        case Direction.Left: X--; break;
                        case Direction.Right: X++; break;
                    }
                    if (Y < headerHeight || Y >= (height - footerHeight - 1) || X <= sideWidth || X >= (width - sideWidth) ||
                        map[X, Y] is Tile.Snake)
                    {
                        //add sound when the snake dies
                        WindowsMediaPlayer crashSound = new WindowsMediaPlayer();
                        crashSound.URL = @"C:\GiaoDienChoiGame\GiaoDienChoiGame\snakeHittingSound.wav";
                        crashSound.controls.play();

                        DisplayGameOver(snake.Count - 1);
                        return;
                    }


                    Console.SetCursorPosition(X, Y);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(DirectionChars[(int)direction!]);
                    Console.ResetColor();
                    snake.Enqueue((X, Y));

                    if (map[X, Y] is Tile.Food)
                    {
                        //handle eating normal food
                        WindowsMediaPlayer eatSound = new WindowsMediaPlayer();
                        eatSound.URL = @"C:\GiaoDienChoiGame\GiaoDienChoiGame\snakeEatingSound.wav";
                        eatSound.controls.play();
                        PositionFood();

                        velocity = Math.Max(10, velocity - 10);  // Increase speed, ensuring it doesn't go below a threshold
                        sleep = TimeSpan.FromMilliseconds(velocity);  // Update sleep time based on new speed

                        normalFoodCounter++;

                        //spawn special food after eating 5 normal foods
                        if (normalFoodCounter % 5 == 0)
                        {
                            PositionSpecialFood();
                        }
                    }
                    else if (specialFoodActive && X == specialX && Y == specialY)
                    {
                        //handle eating special food
                        specialFoodActive = false;

                        map[specialX, specialY] = Tile.Open;
                        Console.SetCursorPosition(specialX, specialY);
                        Console.Write(' ');
                        snake.Enqueue((X, Y)); //the snake grows by 2 more units
                        snake.Enqueue((X, Y));

                        WindowsMediaPlayer specialEatSound = new WindowsMediaPlayer();
                        specialEatSound.URL = @"C:\GiaoDienChoiGame\GiaoDienChoiGame\snakeEatingSound.wav";
                        specialEatSound.controls.play();
                    }
                    else
                    {
                        (int x, int y) = snake.Dequeue();
                        map[x, y] = Tile.Open;
                        Console.SetCursorPosition(x, y);
                        Console.Write(' ');
                    }

                    map[X, Y] = Tile.Snake;

                    //check special food's lifetime
                    if (specialFoodActive)
                    {
                        var timeElapsed = DateTime.Now - specialFoodSpawnTime;

                        if (timeElapsed > specialFoodLifetime)
                        {
                            specialFoodActive = false;
                            Console.SetCursorPosition(specialX, specialY);
                            Console.Write(' ');
                        }
                        else if (timeElapsed > specialFoodLifetime - TimeSpan.FromSeconds(1))
                        {
                            //make the special food blink during the last 1 sec
                            specialFoodBlinking = !specialFoodBlinking;
                            Console.SetCursorPosition(specialX, specialY);
                            Console.ForegroundColor = specialFoodBlinking ? ConsoleColor.Yellow : ConsoleColor.Black;
                            Console.Write('★');
                            Console.ResetColor();
                        }
                    }
                    if (Console.KeyAvailable)
                    {
                        GetDirection(ref direction, ref closeRequested);
                    }

                    DrawConsole(snake.Count - 1, velocity);
                    System.Threading.Thread.Sleep(sleep);
                }
            }
        }
        catch (Exception e)
        {
            exception = e;
            throw;
        }
    }
    static void DisplayBanner()
    {
        string banner = @"
██╗  ██╗██╗   ██╗███╗   ██╗████████╗██╗███╗   ██╗ ██████╗       ██████╗███╗   ██╗ █████╗ ██╗  ██╗███████╗
██║  ██║██║   ██║████╗  ██║╚══██╔══╝██║████╗  ██║██╔════╝      ███╔══╝ ████╗  ██║██╔══██╗██║ ██╔╝██╔════╝
███████║██║   ██║██╔██╗ ██║   ██║   ██║██╔██╗ ██║██║  ███╗      █████  ██╔██╗ ██║███████║█████╔╝ █████╗  
██╔══██║██║   ██║██║╚██╗██║   ██║   ██║██║╚██╗██║██║   ██║          ██ ██║╚██╗██║██╔══██║██╔═██╗ ██╔══╝  
██║  ██║╚██████╔╝██║ ╚████║   ██║   ██║██║ ╚████║╚██████╔╝     ███████╗██║ ╚████║██║  ██║██║  ██╗███████╗
╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝   ╚═╝   ╚═╝╚═╝  ╚═══╝ ╚═════╝      ╚══════╝╚═╝  ╚═══╝╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝";

        // Clear the screen
        Console.Clear();

        // Set text color to dark red
        Console.ForegroundColor = ConsoleColor.DarkRed;

        // Display the banner centered
        CenterTextOnScreen(banner);

        // Reset the default color
        Console.ResetColor();

        // Display the message below the banner, also centered
        string message = "\nPress any key to continue...";
        // Calculate vertical position after banner
        int messageVerticalPosition = Console.WindowHeight / 2 + (banner.Split('\n').Length / 2);
        CenterTextOnScreen(message, messageVerticalPosition);

        // Wait for the user to press any key to continue
        Console.ReadKey(true);
    }

    // Helper function to center text on screen
    static void CenterTextOnScreen(string text, int verticalPosition = -1)
    {
        int consoleWidth = Console.WindowWidth;
        int consoleHeight = Console.WindowHeight;

        // Split the text into lines (if multi-line)
        var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);

        // If verticalPosition is not specified, calculate the vertical center
        if (verticalPosition == -1)
        {
            verticalPosition = (consoleHeight - lines.Length) / 2;
        }

        // Print each line of text centered horizontally
        foreach (var line in lines)
        {
            int horizontalCenter = (consoleWidth - line.Length) / 2;
            Console.SetCursorPosition(horizontalCenter, verticalPosition);
            Console.WriteLine(line);
            verticalPosition++;  // Move to the next line
        }
    }
    // Display the game instructions to the player
    static void DisplayInstructions()
    {
        // Clear the screen for a clean slate to display the instructions 
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkGreen;  // Set text color to dark green

        string instructions = @"
|------------------------------------------------------------------|
|                          Game Instructions:                      | 
|------------------------------------------------------------------|
1. Choose Speed:
Press 1: Slow | 2: Normal (default) | 3: Fast

2. Control Snake:
Use arrow keys:
↑: Up | ↓: Down | ←: Left | →: Right

3. Objective:
Grow the snake by eating food. Game Over if it hits walls or itself.

4. Pause/Resume:
Press Enter to pause or continue.

5. Replay:
After Game Over, press Enter to restart.
--------------------------------------------------------
";

        // Center the text and the frame using the CenterTextOnScreen method
        CenterTextOnScreen(instructions);
        Console.ReadKey(true); // Wait for the user to press any key before selecting the speed.
    }

    // Function to center text horizontally in the console
    static void CenterTextHorizontally(string text)
    {
        int consoleWidth = Console.WindowWidth;  // Get the width of the console window
        int horizontalCenter = (consoleWidth - text.Length) / 2;  // Calculate the center position

        Console.SetCursorPosition(horizontalCenter, Console.CursorTop);  // Set the cursor at the calculated position
        Console.WriteLine(text);  // Write the text at the center
    }

    // Get the player's name
    static string GetPlayerName()
    {
        Console.Clear();

        int consoleWidth = Console.WindowWidth;
        int consoleHeight = Console.WindowHeight;

        // Calculate vertical center position for header
        int verticalCenter = consoleHeight / 2 - 4; // Adjusting to leave some space around

        // Centered header for the game prompt
        Console.ForegroundColor = ConsoleColor.Green;

        string header = "*****************************************";
        Console.SetCursorPosition((consoleWidth - header.Length) / 2, verticalCenter);
        Console.WriteLine(header);

        string title = "*        START HUNTING SNAKE GAME!         *";
        Console.SetCursorPosition((consoleWidth - title.Length) / 2, verticalCenter + 1);
        Console.WriteLine(title);

        string footer = "*****************************************";
        Console.SetCursorPosition((consoleWidth - footer.Length) / 2, verticalCenter + 2);
        Console.WriteLine(footer);

        // Prompt for player name (centered relative to the title)
        string prompt = "Please enter your name:";
        Console.SetCursorPosition((consoleWidth - prompt.Length) / 2, verticalCenter + 4);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(prompt);
        Console.ResetColor();

        // Start from the center of the screen for the name input
        Console.SetCursorPosition(consoleWidth / 2, verticalCenter + 5); // Start from the center horizontally

        // Read the player's name, with the cursor starting from the center and expanding outward
        string? playerName = Console.ReadLine();


        // Return the player's name
        return playerName;
    }
    static void PositionSpecialFood()
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        int headerHeight = 1;
        int footerHeight = 1;
        int sideWidth = 1;
        Tile[,] map = new Tile[width, height];
        List<(int X, int Y)> possibleCoordinates = new();
        for (int i = sideWidth + 1; i <= (width - sideWidth - 1); i++)
        {
            for (int j = headerHeight + 2; j <= (height - footerHeight - 2); j++)
            {
                if (map[i, j] is Tile.Open)
                {
                    possibleCoordinates.Add((i, j));
                }
            }
        }

        if (possibleCoordinates.Count > 0)
        {
            Random random = new Random();
            (int specialX, int specialY) = (-1, -1); //special food variable
            DateTime specialFoodSpawnTime = DateTime.MinValue;
            TimeSpan specialFoodLifetime = TimeSpan.FromSeconds(7);
            bool specialFoodActive;
            (specialX, specialY) = possibleCoordinates[random.Next(possibleCoordinates.Count)];
            specialFoodActive = true;
            specialFoodSpawnTime = DateTime.Now;

            Console.SetCursorPosition(specialX, specialY);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write('★'); //special food's icon
            Console.ResetColor();
        }
    }

    static void WaitForInputAndBlink()
    {
        int blinkInterval = 300; //interval between blinks (ms)
        DateTime lastBlinkTime = DateTime.Now;
        bool isVisible = true;
        Direction? direction = null;
        bool closeRequested = false;
        (int X, int Y) = (2, 1);

        //draw a blinking triangle at the current position (X, Y)
        while (!direction.HasValue && !closeRequested)
        {
            DateTime currentTime = DateTime.Now;

            //check if it's time to blink
            if ((currentTime - lastBlinkTime).TotalMilliseconds >= blinkInterval)
            {
                isVisible = !isVisible;  //toggle the visibility state of the triangle
                lastBlinkTime = currentTime;

                //clear the triangle before redrawing it
                Console.SetCursorPosition(X, Y);
                Console.Write(' ');

                //if the triangle is still visible, draw it
                if (isVisible)
                {
                    Console.SetCursorPosition(X, Y);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("▶︎");
                    Console.ResetColor();
                }
            }

            //check if the player has pressed a movement key
            if (Console.KeyAvailable)
            {
                GetDirection(ref direction, ref closeRequested);
            }

            System.Threading.Thread.Sleep(50);
        }
    }
    static void GetDirection(ref Direction? direction, ref bool closeRequested)
    {
        switch (Console.ReadKey(true).Key)
        {
            case ConsoleKey.UpArrow: direction = Direction.Up; break;
            case ConsoleKey.DownArrow: direction = Direction.Down; break;
            case ConsoleKey.LeftArrow: direction = Direction.Left; break;
            case ConsoleKey.RightArrow: direction = Direction.Right; break;
            case ConsoleKey.Escape: closeRequested = true; break;
        }
    }
    static void PositionFood()
    {
        Random random = new Random();
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        int headerHeight = 1;
        int footerHeight = 1;
        int sideWidth = 1;

        char[] listOfFoodChars = { '◉', '◆', '●', '◈' };

        Tile[,] map = new Tile[width, height];
        List<(int X, int Y)> possibleCoordinates = new();
        for (int i = sideWidth + 1; i <= (width - sideWidth - 1); i++)
        {
            for (int j = headerHeight + 2; j <= (height - footerHeight - 2); j++)
            {
                if (map[i, j] is Tile.Open)
                {
                    possibleCoordinates.Add((i, j));
                }
            }
        }

        if (possibleCoordinates.Count > 0)
        {
            var (X, Y) = possibleCoordinates[random.Next(possibleCoordinates.Count)];
            map[X, Y] = Tile.Food;

            char foodChar = listOfFoodChars[random.Next(listOfFoodChars.Length)];
            var colors = new List<ConsoleColor> { ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.DarkYellow };
            Console.SetCursorPosition(X, Y);
            Console.ForegroundColor = colors[random.Next(colors.Count)];
            Console.Write(foodChar);
            Console.ResetColor();
        }
    }
    static void DrawConsole(int score, int velocity)
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        string title = "[HUNTING SNAKE]🐍";
        string speed = velocity == 100 ? "Slow" : velocity == 70 ? "Normal" : "Fast";
        string headerSpeed = $"Speed: {speed}";
        string footerPause = "[Space]: Pause the game";
        string footerScore = $"Score: {score}";

        //top border
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.SetCursorPosition(1, 0);
        Console.Write('╔' + new string('═', width - 3) + '╗');

        //header
        Console.SetCursorPosition((width - title.Length) / 2, 0);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(title);

        Console.SetCursorPosition(width - headerSpeed.Length - 2, 0);
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write(headerSpeed);

        //bottom border
        Console.SetCursorPosition(1, height - 2);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write('╚' + new string('═', width - 3) + '╝');

        //side borders
        for (int y = 1; y < height - 2; y++)
        {
            Console.SetCursorPosition(1, y);
            Console.Write('║');
            Console.SetCursorPosition(width - 1, y);
            Console.Write('║');
        }

        //footer
        Console.SetCursorPosition(2, height - 1);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[ESC]: Exit");

        Console.SetCursorPosition((width - footerPause.Length) / 2, height - 1);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(footerPause);

        Console.SetCursorPosition(width - footerScore.Length - 3, height - 1);
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"{footerScore} 🌟");

        //reset color
        Console.ResetColor();
    }
    static void DisplayGameOver(int score)
    {
        Console.Clear();
        // Set the color for the "GAME OVER" text
        Console.ForegroundColor = ConsoleColor.DarkRed;
        string gameOverMessage = @"


░██████╗░░█████╗░███╗░░░███╗███████╗  ░█████╗░██╗░░░██╗███████╗██████╗░
██╔════╝░██╔══██╗████╗░████║██╔════╝  ██╔══██╗██║░░░██║██╔════╝██╔══██╗
██║░░██╗░███████║██╔████╔██║█████╗░░  ██║░░██║╚██╗░██╔╝█████╗░░██████╔╝
██║░░╚██╗██╔══██║██║╚██╔╝██║██╔══╝░░  ██║░░██║░╚████╔╝░██╔══╝░░██╔══██╗
╚██████╔╝██║░░██║██║░╚═╝░██║███████╗  ╚█████╔╝░░╚██╔╝░░███████╗██║░░██║
░╚═════╝░╚═╝░░╚═╝╚═╝░░░░░╚═╝╚══════╝  ░╚════╝░░░░╚═╝░░░╚══════╝╚═╝░░╚═╝

Your final score: " + (score == 0 ? "0 " : score.ToString());

        // Display the GAME OVER message centered on the screen
        CenterTextOnScreen(gameOverMessage);

        string prompt = "Press Enter to play again, or Press any other keys to Escape.";
        CenterTextHorizontally(prompt);

        // Wait for the player to press Enter to play again or Escape to exit
        ConsoleKey key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Enter)
        {
            Main();
        }
        else if (key == ConsoleKey.Escape)
        {
            // Exit the game
            return;
        }
    }
    enum Direction
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    enum Tile
    {
        Open = 0,
        Snake,
        Food,
    }
}
