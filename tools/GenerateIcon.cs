// パッケージアイコン icon.png(128x128)を生成する。
// 実行: dotnet run tools/GenerateIcon.cs -- icon.png
//
// このライブラリでカスタマバーコードを作り、先頭4シンボル(スタート + 郵便番号3桁)のバー配置(GetBars)を
// 横2倍・縦7倍に伸ばして、紺の角丸の背景に白で描く(ライブラリの出力は白地に黒だが、アイコンでは色を反転している)。
// バーのある範囲を上下左右の中央に置く。
#:project ../ZipConvertCustomerBarCode.SkiaSharp/ZipConvertCustomerBarCode.SkiaSharp.csproj
using SkiaSharp;
using ZipConvertCustomerBarCode;

const int Size = 128;
const int ScaleX = 2;
const int ScaleY = 7;
const int Symbols = 4;
const int CellWidth = 12;
var background = new SKColor(0x1F, 0x3A, 0x5F);

var code = BarCode.Create("2630023", "千葉市稲毛区緑町3丁目30-8　郵便ビル403号");
var bars = code.GetBars().Where(b => b.X < Symbols * CellWidth).ToList();

// バーのある範囲(スタート記号の先頭は空き枠なので、シンボルの枠ではなくバーで測る)
var left = bars.Min(b => b.X) * ScaleX;
var right = bars.Max(b => b.X + b.Width) * ScaleX;
var top = bars.Min(b => b.Y) * ScaleY;
var bottom = bars.Max(b => b.Y + b.Height) * ScaleY;
var offsetX = (Size - (right - left)) / 2 - left;
var offsetY = (Size - (bottom - top)) / 2 - top;

using var bitmap = new SKBitmap(Size, Size);
using var canvas = new SKCanvas(bitmap);
canvas.Clear(SKColors.Transparent);
using var backgroundPaint = new SKPaint { Color = background, IsAntialias = true };
canvas.DrawRoundRect(new SKRect(0, 0, Size, Size), 20, 20, backgroundPaint);
using var barPaint = new SKPaint { Color = SKColors.White };
foreach (var bar in bars)
{
    canvas.DrawRect(offsetX + bar.X * ScaleX, offsetY + bar.Y * ScaleY, bar.Width * ScaleX, bar.Height * ScaleY, barPaint);
}

using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
File.WriteAllBytes(args.Length > 0 ? args[0] : "icon.png", data.ToArray());
Console.WriteLine($"バーの範囲: x={offsetX + left}〜{offsetX + right - 1}, y={offsetY + top}〜{offsetY + bottom - 1}");
