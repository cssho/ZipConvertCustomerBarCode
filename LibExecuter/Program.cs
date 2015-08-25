using System.Drawing;
using ZipConvertCustomerBarCode;

namespace LibExecuter
{
    class Program
    {

        static void Main(string[] args)
        {
            Bitmap img = BarCode.CreateImage("0140113", "秋田県大仙市堀見内　南田茂木　添60-1");
        }
    }
}
