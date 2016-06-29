using System;
using System.Collections.Generic;
using System.Text;
using UeiDaq;

namespace ConsoleApplication1
{
    class Program
    {
        static void InitializeSyncLines(string resource)
        {
            uint reg = 0x0080;
            uint data = 0x80808080;

            try
            {
                DeviceCollection dc = new DeviceCollection(resource);
                foreach (Device dev in dc)
                {
                    if (dev != null) dev.WriteRegister32(reg, data);
                }
            }
            catch (UeiDaqException exception)
            {
                Console.WriteLine("Error: (" + exception.Error + ") " + exception.Message);
            }
        }
        static void Main(string[] args)
        {
            try
            {
                Device cpuDev = DeviceEnumerator.GetDeviceFromResource("pdna://192.168.100.2/dev14");
                //Device aiDev = DeviceEnumerator.GetDeviceFromResource("pdna://192.168.100.2/dev1");
                Session aisession = new Session();
                aisession.CreateAIChannel("pdna://192.168.100.2/dev0/ai0", -10, 10, AIChannelInputMode.Differential);
                aisession.ConfigureTimingForSimpleIO();
                aisession.Start();
                Device aiDev = aisession.GetDevice();

                InitializeSyncLines("pdna://192.168.100.2/");

                // 2.1.2.2 on the "Layer A" configure start trigger to use software trigger a source with rising edge (TSRC = 1)
                uint reg = 0x0080;
                uint data = 0x1;
                cpuDev.WriteRegister32(reg, data);

                // 2.1.2.3 on one of the "Layer A" configure selected SYNC line to carry start trigger detected strobe (SYNC_SOURCE = 7)
                reg = 0x0080;
                data = 0x00000087;
                cpuDev.WriteRegister32(reg, data);

                // 2.1.2.4 on the "Layer B" configure start trigger to use selected SYNC line
                reg = 0x0098;
                data = 12;
                aiDev.WriteRegister32(reg, data);
                // 2.1.2.5 on the "Layer B" configure Start Trigger Interrupt (STT) in the interrupt register
                reg = 0x0020;
                data = 1U << 18;
                aiDev.WriteRegister32(reg, data);
                // 2.1.2.6 check STT interrupt status on the "Layer B", it should be 0 (no IRQ)
                reg = 0x0024;
                uint val = aiDev.ReadRegister32(reg);
                //Try to reset STT if it is set
                if (val != 0)
                {
                    Console.WriteLine("STT is set, trying to reset...");
                    aiDev.WriteRegister32(reg, data);
                    val = aiDev.ReadRegister32(reg);
                }
                //If STT can't be reset return
                if (val != 0)
                {
                    Console.WriteLine("Could not reset STT");
                }

                // 2.1.2.7 issue software start trigger on the "Layer A" (read from TSCFG base+ 0x0098)
                reg = 0x0098;
                val = cpuDev.ReadRegister32(reg);
                // 2.1.2.8 check STT interrupt status on the "Layer B", it should be 1 (IRQ)            
                reg = 0x0024;
                val = aiDev.ReadRegister32(reg);

                // 2.1.2.9 clear STT interrupt and check that it stays at 0 on the "Layer B"
                if (val != 0)
                {
                    Console.WriteLine("STT interrupt detected");
                    aiDev.WriteRegister32(reg, data);
                    val = aiDev.ReadRegister32(reg);
                }

                aisession.Dispose();
            }
            catch (UeiDaqException exception)
            {
                Console.WriteLine("Error: (" + exception.Error + ") " + exception.Message);
            }
        }
    }
}
