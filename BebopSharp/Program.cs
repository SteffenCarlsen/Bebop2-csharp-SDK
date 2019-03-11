using System;

namespace BebopSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            
            //This is a sample about using the pilotting command.

            Bebop bebop = new Bebop(0);
            if (bebop.Connect() == ConnectionStatus.Failed)
            {
                Console.ReadLine();
                return;
            }
            else
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    switch (input)
                    {
                        //takeoff
                        case "t":
                            bebop.Takeoff();
                            break;
                        //landing
                        //moving command: -100% ~ 100%
                        case "l":
                            bebop.Landing();
                            break;
                        //left
                        case "a":
                            bebop.Move(1, -10, 0, 0, 0);
                            break;
                        //right
                        case "d":
                            bebop.Move(1, 10, 0, 0, 0);
                            break;
                        //forward
                        case "w":
                            bebop.Move(1, 0, 10, 0, 0);
                            break;
                        //backward
                        case "s":
                            bebop.Move(1, 0, -10, 0, 0);
                            break;
                        //turn left
                        case "h":
                            bebop.Move(0, 0, 0, -10, 0);
                            break;
                        //turn right
                        case "k":
                            bebop.Move(0, 0, 0, 10, 0);
                            break;
                        //up
                        case "u":
                            bebop.Move(0, 0, 0, 0, 10);
                            break;
                        //down
                        case "j":
                            bebop.Move(0, 0, 0, 0, -10);
                            break;
                        //pause
                        case "p":
                            bebop.Move(0, 0, 0, 0, 0);
                            break;
                        case "v":
                            bebop.VideoEnable(); //enable RTP/.H264 videostreaming
                            break;
                        //quit
                        case "q":
                            bebop.CancelAllTasks();
                            return;
                    }
                }
            }
        }
    }
}
