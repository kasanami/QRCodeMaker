using Microsoft.Win32;
using QRCodeMaker.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static QRCodeMaker.Core.QRCode;

namespace QRCodeMaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 生成した画像
        /// </summary>
        Bitmap bitmap;


        public MainWindow()
        {
            InitializeComponent();

            textBox1.AcceptsReturn = true;
            textBox1.TextWrapping = TextWrapping.Wrap;

            eclComboBox.ItemsSource = Ksnm.Enum.GetValues<ErrorCorrectionLevel>();
            eclComboBox.SelectedItem = ErrorCorrectionLevel.High;

            versionComboBox.ItemsSource = Enumerable.Range(QRCode.MinVersion, QRCode.MaxVersion).ToArray();
            versionComboBox.SelectedItem = QRCode.MinVersion;

            shapeComboBox.ItemsSource = Ksnm.Enum.GetValues<QRCode.ModuleShape>();
            shapeComboBox.SelectedItem = QRCode.ModuleShape.Square;

            // 画像を生成するまでは無効化
            saveButton.IsEnabled = false;
        }

        /// <summary>
        /// QRコードを生成
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var text = textBox1.Text;
            var bytes = new List<byte>(Encoding.UTF8.GetBytes(text));

            var ecl = (ErrorCorrectionLevel)eclComboBox.SelectedItem;
            var version = (int)versionComboBox.SelectedItem;
            var color = ColorTranslator.FromHtml("#" + colorTextBox.Text);
            var size = int.Parse( sizeTextBox.Text);
            var scale = float.Parse(scaleTextBox.Text) / 100;
            var shape = (QRCode.ModuleShape)shapeComboBox.SelectedItem;
            var qrCode = QRCode.EncodeBinary(bytes, ecl, version, QRCode.MaxVersion, AutoMask, false);

            bitmap = qrCode.ToImage(shape, size, scale, 1, color);

            // 表示
            {
                IntPtr hbitmap = bitmap.GetHbitmap();
                qrCodeImage.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hbitmap);
                // 保存可能にする
                saveButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// gdi32.dllのDeleteObjectメソッド
        /// </summary>
        /// <param name="hObject"></param>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// 数値のみを入力に許可
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hexadecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                Regex regex = new Regex("[^0-9A-Fa-f]");
                e.Handled = regex.IsMatch(e.Text);
            }
        }

        /// <summary>
        /// 画像を保存
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 画像を生成していないなら何もしない
            if (qrCodeImage.Source == null)
            {
                return;
            }

            var dialog = new SaveFileDialog();
            dialog.Title = "名前を付けて保存";
            dialog.Filter = "PNG(*.png)|*.png|JPEG(*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP(*.bmp)|*.bmp";
            dialog.DefaultExt = ".png";
            dialog.AddExtension = true;
            var result = dialog.ShowDialog() ?? false;
            if (result)
            {
                ImageFormat imageFormat = ImageFormat.Png;
                if (dialog.FilterIndex == 1) { imageFormat = ImageFormat.Png; }
                else if (dialog.FilterIndex == 2) { imageFormat = ImageFormat.Jpeg; }
                else if (dialog.FilterIndex == 3) { imageFormat = ImageFormat.Bmp; }
                else { throw new Exception("拡張子が選択されていません。"); }
                bitmap.Save(dialog.FileName, imageFormat);
            }
        }
    }
}