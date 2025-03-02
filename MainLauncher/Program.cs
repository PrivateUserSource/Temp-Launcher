using System;
using System.IO;
using System.Diagnostics;
using IniParser;
using IniParser.Model;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace FortniteLauncherApp
{
    class Program
    {
        private static string buildFilePath = "./build.ini";
        private static string asciiArt = @"
 ______    _______  _     _  ___   __    _  ______  
|    _ |  |       || | _ | ||   | |  |  | ||      | 
|   | ||  |    ___|| || || ||   | |   |_| ||  _    |
|   |_||_ |   |___ |       ||   | |       || | |   |
|    __  ||    ___||       ||   | |  _    || |_|   |
|   |  | ||   |___ |   _   ||   | | | |   ||       |
|___|  |_||_______||__| |__||___| |_|  |__||______| 
";

        private static string options = @"
Options:
1: Add Build
2: Add Credentials
3: Launch Game
4: Close Fortnite
";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        private const uint THREAD_SUSPEND_RESUME = 0x0002;
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 4;

        static async Task Main(string[] args)
        {
            ShowMenu();
            await HandleUserChoice();
        }

        private static void ShowMenu()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(asciiArt);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(options);
        }

        static async Task HandleUserChoice()
        {
            string answer = Console.ReadLine().Trim();

            switch (answer)
            {
                case "1":
                    await AddBuild();
                    break;
                case "2":
                    await AddCredentials();
                    break;
                case "3":
                    await LaunchGame();
                    break;
                case "4":
                    CloseFortnite();
                    break;
                default:
                    Console.WriteLine("Invalid option. Please choose 1, 2, 3, or 4.");
                    await HandleUserChoice();
                    break;
            }
        }

        static async Task AddBuild()
        {
            if (File.Exists(buildFilePath))
            {
                var parser = new FileIniDataParser();
                IniData config = parser.ReadFile(buildFilePath);
                string existingBuildPath = config["build"]?["path"];

                if (!string.IsNullOrEmpty(existingBuildPath))
                {
                    Console.WriteLine($"There is already a build located at {existingBuildPath}. Would you like to remove the old build and add a new one? (yes/no): ");
                    string answer = Console.ReadLine().Trim().ToLower();

                    if (answer == "yes")
                    {
                        RemoveOldBuild(() =>
                        {
                            AskForNewBuild();
                        });
                    }
                    else
                    {
                        Console.WriteLine("Returning to the menu...");
                        await HandleUserChoice();
                    }
                }
                else
                {
                    AskForNewBuild();
                }
            }
            else
            {
                AskForNewBuild();
            }
        }

        static async Task AskForNewBuild()
        {
            Console.WriteLine("Enter the path of the build: ");
            string buildPath = Console.ReadLine().Trim();

            var parser = new FileIniDataParser();
            IniData buildData = new IniData();

            if (File.Exists(buildFilePath))
            {
                buildData = parser.ReadFile(buildFilePath);
            }

            var buildSection = new SectionData("build");
            buildSection.Keys.AddKey("path", buildPath);

            buildData.Sections.Add(buildSection);

            parser.WriteFile(buildFilePath, buildData);
            Console.WriteLine("Build path has been saved!");
            await HandleUserChoice();
        }

        static void RemoveOldBuild(Action callback)
        {
            Console.WriteLine("Removing old build...");
            if (File.Exists(buildFilePath))
            {
                var parser = new FileIniDataParser();
                IniData config = parser.ReadFile(buildFilePath);
                config.Sections.RemoveSection("build");
                parser.WriteFile(buildFilePath, config);
                Console.WriteLine("Old build removed.");
            }

            callback();
        }

        static async Task AddCredentials()
        {
            var parser = new FileIniDataParser();
            IniData config = new IniData();

            string email = "";
            string password = "";

            Console.WriteLine("Enter your email: ");
            email = Console.ReadLine().Trim();

            Console.WriteLine("Enter your password: ");
            password = Console.ReadLine().Trim();

            if (File.Exists(buildFilePath))
            {
                config = parser.ReadFile(buildFilePath);
            }

            var credentialsSection = new SectionData("credentials");
            credentialsSection.Keys.AddKey("email", email);
            credentialsSection.Keys.AddKey("password", password);

            config.Sections.Add(credentialsSection);

            parser.WriteFile(buildFilePath, config);
            Console.WriteLine("Credentials have been saved!");
            await HandleUserChoice();
        }

        static async Task LaunchGame()
        {
            Console.Clear();
            string email = "";
            string password = "";

            if (File.Exists(buildFilePath))
            {
                var parser = new FileIniDataParser();
                IniData config = parser.ReadFile(buildFilePath);
                string buildPath = config["build"]?["path"];

                email = config["credentials"]?["email"];
                password = config["credentials"]?["password"];

                if (string.IsNullOrEmpty(buildPath))
                {
                    Console.WriteLine("No build path saved! Please add a build first.");
                    await HandleUserChoice();
                    return;
                }

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("No credentials found! Please provide your email and password.");
                    await AddCredentials();
                    return;
                }

                Console.WriteLine($"Launching game with build located at: {buildPath}");
                Launch(buildPath, email, password);
            }
            else
            {
                Console.WriteLine("No build saved. Please add a build first.");
                await HandleUserChoice();
            }
        }

        static void Launch(string buildPath, string email, string password)
        {
            string launcherPath = Path.Combine(buildPath, "FortniteGame\\Binaries\\Win64\\FortniteLauncher.exe");
            string eac_path = Path.Combine(buildPath, "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_EAC.exe");
            string bePath = Path.Combine(buildPath, "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_BE.exe");
            string clientPath = Path.Combine(buildPath, "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe");

            try
            {
                Console.WriteLine("Launching FortniteClient...");
                Process process = Process.Start(launcherPath);
                SuspendProcess(process);

                Process eac = Process.Start(eac_path);
                SuspendProcess(eac);

                Process be = Process.Start(bePath);
                SuspendProcess(be);

                string clientArgs = $"-AUTH_TYPE=epic -auth_login={email} -auth_password={password} -epicapp=Fortnite -epicenv=Prod -epiclocale=en-us -epicportal -skippatchcheck -nobe -fromfl=eac -fltoken=3db3ba5dcbd2e16703f3978d -caldera=eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJhY2NvdW50X2lkIjoiYmU5ZGE1YzJmYmVhNDQwN2IyZjQwZWJhYWQ4NTlhZDQiLCJnZW5lcmF0ZWQiOjE2Mzg3MTcyNzgsImNhbGRlcmFHdWlkIjoiMzgxMGI4NjMtMmE2NS00NDU3LTliNTgtNGRhYjNiNDgyYTg2IiwiYWNQcm92aWRlciI6IkVhc3lBbnRpQ2hlYXQiLCJub3RlcyI6IiIsImZhbGxiYWNrIjpmYWxzZX0.VAWQB67RTxhiWOxx7DBjnzDnXyyEnX7OljJm-j2d88G_WgwQ9wrE6lwMEHZHjBd1ISJdUO1UVUqkfLdU5nofBQ";
                Process FortniteClient = Process.Start(clientPath, clientArgs);
                FortniteClient.WaitForInputIdle();
                ShowMenu();
                HandleUserChoice();
                Console.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching Fortnite: {ex.Message}");
            }
        }

        static void SuspendProcess(Process process)
        {
            try
            {
                foreach (ProcessThread thread in process.Threads)
                {
                    IntPtr threadHandle = OpenThread(THREAD_SUSPEND_RESUME, false, (uint)thread.Id);
                    if (threadHandle != IntPtr.Zero)
                    {
                        SuspendThread(threadHandle);
                        CloseHandle(threadHandle);
                    }
                }

                Console.WriteLine($"FortniteLauncher in suspension state!");
            }
            catch (Exception ex)
            {
                CloseFortnite();
                Console.WriteLine($"Error suspending process: {ex.Message}");
            }
        }

        static void CloseFortnite()
        {
            foreach (var process in Process.GetProcessesByName("FortniteLauncher"))
            {
                try
                {
                    process.Kill();
                    Console.WriteLine($"Closed FortniteLauncher with PID {process.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing FortniteLauncher with PID {process.Id}: {ex.Message}");
                }
            }

            Task.Run(() => HandleUserChoice());
        }
    }
}
