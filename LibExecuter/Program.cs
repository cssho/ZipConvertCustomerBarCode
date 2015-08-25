using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZipConvertCustomerBarCode;

namespace LibExecuter
{
    class Program
    {

        static void Main(string[] args)
        {
            while (true)
            {
                Console.ReadKey();
                BarCode.Export("0140113", "秋田県大仙市堀見内　南田茂木　添60-1",@"c:\test.gif");
            }
        }
    }
}
