using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace ML
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private PictureBox pictureBox;
        private Button btnLoad, btnGrayscale, btnThreshold, btnSobel, btnPredict, btnResize28;
        private OpenFileDialog ofd;
        private Bitmap current;
        private Label lblPrediction;
        private ComboBox cbThreshold;

        private float[][] templates;
        private int templateSize = 28;

        public MainForm()
        {
            Text = "Mini ML Math Image Processor (.NET 8)";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            InitComponents();
            templates = GenerateDigitTemplates(templateSize, templateSize);
        }

        private void InitComponents()
        {
            pictureBox = new PictureBox
            {
                Left = 10,
                Top = 10,
                Width = 640,
                Height = 480,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Controls.Add(pictureBox);

            btnLoad = new Button { Left = 670, Top = 10, Width = 280, Height = 35, Text = "Load Image" };
            btnLoad.Click += (s, e) => LoadImage();
            Controls.Add(btnLoad);

            btnGrayscale = new Button { Left = 670, Top = 55, Width = 280, Height = 35, Text = "Grayscale" };
            btnGrayscale.Click += (s, e) => { if (current != null) { current = ToGrayscale(current); pictureBox.Image = current; } };
            Controls.Add(btnGrayscale);

            btnThreshold = new Button { Left = 670, Top = 100, Width = 140, Height = 35, Text = "Threshold" };
            btnThreshold.Click += (s, e) => { if (current != null) { current = Threshold(current, GetSelectedThreshold()); pictureBox.Image = current; } };
            Controls.Add(btnThreshold);

            cbThreshold = new ComboBox { Left = 820, Top = 100, Width = 130 };
            cbThreshold.Items.AddRange(new object[] { "128", "100", "150", "180", "200" });
            cbThreshold.SelectedIndex = 0;
            Controls.Add(cbThreshold);

            btnSobel = new Button { Left = 670, Top = 145, Width = 280, Height = 35, Text = "Sobel Edges" };
            btnSobel.Click += (s, e) => { if (current != null) { current = SobelEdges(ToGrayscale(current)); pictureBox.Image = current; } };
            Controls.Add(btnSobel);

            btnResize28 = new Button { Left = 670, Top = 190, Width = 280, Height = 35, Text = "Resize to 28x28" };
            btnResize28.Click += (s, e) => { current = ResizeBitmap(current, templateSize, templateSize); pictureBox.Image = current; };
            Controls.Add(btnResize28);

            btnPredict = new Button { Left = 670, Top = 235, Width = 280, Height = 35, Text = "Predict Digit (Simple ML)" };
            btnPredict.Click += (s, e) =>
            {
                if (current != null)
                {
                    lblPrediction.Text = "Prediction: " + PredictCurrentDigit();
                }
            };
            Controls.Add(btnPredict);

            lblPrediction = new Label
            {
                Left = 670,
                Top = 280,
                Width = 280,
                Height = 35,
                Text = "Prediction: -",
                Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold)
            };
            Controls.Add(lblPrediction);

            var help = new Label
            {
                Left = 670,
                Top = 330,
                Width = 280,
                Height = 170,
                Text = "Workflow:\n1) Load Image\n2) Grayscale → Threshold/Sobel\n3) Resize 28x28\n4) Predict\n(Simple template matching ML)"
            };
            Controls.Add(help);

            ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        }

        private void LoadImage()
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                current = new Bitmap(ofd.FileName);
                pictureBox.Image = current;
            }
        }

        private int GetSelectedThreshold()
        {
            int.TryParse(cbThreshold.SelectedItem?.ToString(), out int t);
            return Math.Clamp(t == 0 ? 128 : t, 0, 255);
        }

        // --- Image Processing ---

        private Bitmap ToGrayscale(Bitmap src)
        {
            var bmp = new Bitmap(src.Width, src.Height);
            using var g = Graphics.FromImage(bmp);

            var cm = new ColorMatrix(new float[][]
            {
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
            });

            var ia = new ImageAttributes();
            ia.SetColorMatrix(cm);

            g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height),
                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);

            return bmp;
        }

        private Bitmap Threshold(Bitmap src, int t)
        {
            var gray = ToGrayscale(src);
            var bmp = new Bitmap(gray.Width, gray.Height);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixel = gray.GetPixel(x, y).R;
                    bmp.SetPixel(x, y, pixel < t ? Color.Black : Color.White);
                }
            }

            return bmp;
        }

        private Bitmap ResizeBitmap(Bitmap src, int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
            return bmp;
        }

        private Bitmap SobelEdges(Bitmap gray)
        {
            int w = gray.Width; int h = gray.Height;
            var output = new Bitmap(w, h);

            int[,] gx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] gy = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int sx = 0, sy = 0;

                    for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int val = gray.GetPixel(x + kx, y + ky).R;
                            sx += gx[ky + 1, kx + 1] * val;
                            sy += gy[ky + 1, kx + 1] * val;
                        }

                    int mag = Math.Min(255, (int)Math.Sqrt(sx * sx + sy * sy));
                    output.SetPixel(x, y, Color.FromArgb(mag, mag, mag));
                }
            }

            return output;
        }

        // --- Simple ML (Template Matching) ---

        private float[][] GenerateDigitTemplates(int w, int h)
        {
            var arr = new float[10][];

            for (int d = 0; d <= 9; d++)
            {
                using var bmp = new Bitmap(w, h);
                using var g = Graphics.FromImage(bmp);

                g.Clear(Color.White);

                var f = new Font("Arial", w * 0.9f, FontStyle.Bold, GraphicsUnit.Pixel);
                var size = g.MeasureString(d.ToString(), f);

                g.DrawString(d.ToString(), f, Brushes.Black,
                    (w - size.Width) / 2, (h - size.Height) / 2);

                var bin = Threshold(bmp, 150);
                arr[d] = BitmapToVector(bin);
            }

            return arr;
        }

        private float[] BitmapToVector(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            float[] v = new float[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    v[y * w + x] = (255 - bmp.GetPixel(x, y).R) / 255f;
                }

            return v;
        }

        private int PredictCurrentDigit()
        {
            var img = ResizeBitmap(current, templateSize, templateSize);
            var bin = Threshold(img, 150);
            var fv = BitmapToVector(bin);

            int best = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < templates.Length; i++)
            {
                double dist = 0;

                for (int k = 0; k < fv.Length; k++)
                {
                    double diff = fv[k] - templates[i][k];
                    dist += diff * diff;
                }

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }
    }
}