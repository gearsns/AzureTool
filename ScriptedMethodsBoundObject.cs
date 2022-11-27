using CefSharp;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Xml.Linq;

namespace AzureaTool
{
    public class ScriptedMethodsBoundObject
    {
        private IJavascriptCallback? AddMessage = null;
        private string current_device = "";
        private string pvp_result = "";
        private double wait_check_time = 0;
        private Dictionary<string, Mat> img_caches = new();
        private Dictionary<string, Mat> match_img_caches = new();
        private Dictionary<string, Mat> match_img_hash_caches = new();
        private AKAZE detector = AKAZE.Create();
        private BFMatcher bf = new(NormTypes.Hamming);
        private OpenCvSharp.ImgHash.RadialVarianceHash hash_func = OpenCvSharp.ImgHash.RadialVarianceHash.Create();
        private string adb_path = Properties.Settings.Default.adb_path;
        private string data_path = Properties.Settings.Default.data_path;
        private bool DebugCapture = false;

        public delegate void addLog(string mes);
        public addLog? AddLog;

        public ScriptedMethodsBoundObject()
        {
            if (adb_path.Length == 0)
            {
                adb_path = @"F:/Program/Nox/bin/nox_adb";
            }
            if (data_path.Length == 0)
            {
                data_path = @".";
            }
        }
        public void SetAddMessage(IJavascriptCallback callback)
        {
            AddMessage = callback;
        }

        public string GetSettingValue(string key)
        {
            switch (key)
            {
                case "adb_path":
                    return adb_path;
                case "data_path":
                    return data_path;
            }
            return "";
        }
        public void SetSettingValue(string key, string value)
        {
            switch (key)
            {
                case "adb_path":
                    adb_path = value;
                    Properties.Settings.Default.adb_path = value;
                    break;
                case "data_path":
                    if(value.Length == 0)
                    {
                        value = ".";
                    }
                    data_path = value;
                    Properties.Settings.Default.data_path = value;
                    break;
            }
            Properties.Settings.Default.Save();
        }
        public class OpenFileDialogState
        {
            public DialogResult result;
            public OpenFileDialog dialog;
            public OpenFileDialogState(OpenFileDialog dialog)
            {
                this.dialog = dialog;
            }

            public void ThreadProcShowDialog()
            {
                result = dialog.ShowDialog();
            }
        }
        public class CommonOpenFileDialogState
        {
            public CommonFileDialogResult result;
            public CommonOpenFileDialog dialog;
            public CommonOpenFileDialogState(CommonOpenFileDialog dialog)
            {
                this.dialog = dialog;
            }

            public void ThreadProcShowDialog()
            {
                result = dialog.ShowDialog();
            }
        }
        private DialogResult STAShowOpenFileDialog(OpenFileDialog dialog)
        {
            OpenFileDialogState state = new (dialog);
            System.Threading.Thread t = new (state.ThreadProcShowDialog);
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            return state.result;
        }
        private CommonFileDialogResult STAShowCommonOpenFileDialog(CommonOpenFileDialog dialog)
        {
            CommonOpenFileDialogState state = new (dialog);
            System.Threading.Thread t = new (state.ThreadProcShowDialog);
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            return state.result;
        }
        public string GetLocalFile(string title, string path)
        {
            OpenFileDialog d = new()
            {
                Title = title,
                FileName = path,
            };
            DialogResult ret = STAShowOpenFileDialog(d);

            return ret == DialogResult.OK ? d.FileName : "";
        }
        public string GetLocalFolder(string title, string path)
        {
            CommonOpenFileDialog d = new()
            {
                Title = title,
                IsFolderPicker = true,
                DefaultDirectory = path,
            };
            CommonFileDialogResult ret = STAShowCommonOpenFileDialog(d);

            return ret == CommonFileDialogResult.Ok ? d.FileName : "";
        }

        public Mat? capture_screen()
        {
            if (DebugCapture)
            {
                return Cv2.ImRead($"{data_path}/debug.png");
            } else
            {
                var r = exec_adb(new string[] { "exec-out", "screencap" });
                if (r == null || r.Length <= 13)
                {
                    return null;
                }
                var width = BitConverter.ToInt32(r, 0);
                var height = BitConverter.ToInt32(r, 4);
                var data = new Mat(height, width, MatType.CV_8UC4, r.Skip(16).ToArray());
                Cv2.CvtColor(data, data, ColorConversionCodes.BGRA2RGBA);
                return data;
            }
        }
        public byte[] exec_adb(string[] args, string device = "")
        {
            List<string> argsList = new();
            if (device.Length > 0)
            {
                argsList.Add("-s");
                argsList.Add(device);
            }
            else if (current_device.Length > 0)
            {
                argsList.Add("-s");
                argsList.Add(current_device);
            }
            argsList.AddRange(args);
            var startInfo = new ProcessStartInfo()
            {
                FileName = adb_path, // @"F:/Program/Nox/bin/nox_adb",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var item in argsList)
            {
                startInfo.ArgumentList.Add(item);
            }
            using var ps = Process.Start(startInfo);
            var reader = new BinaryReader(ps!.StandardOutput.BaseStream);
            List<byte> list = new();
            while (true)
            {
                var r = reader.ReadBytes((int)100);
                list.AddRange(r);
                if (r.Length < 100)
                {
                    break;
                }
            }
            //ps!.WaitForExit();
            return list.ToArray();
        }
        public string save_screen()
        {
            string basefilename = $"output/{DateTime.Now:yyyyyMMddHHmmss}.png";
            string filename = $@"contents/gears.azure.app/{basefilename}";
            byte[] r = exec_adb(new string[] { "exec-out", "screencap", "-p" });
            using FileStream f = File.OpenWrite(filename);
            f.Write(r);
            return basefilename;
        }
        public string save_screen_ext(string name, string device = "")
        {
            string basefilename =$"saveimage/{name}.png";
            string filename = $@"contents/gears.azure.app/{basefilename}";
            byte[] r = exec_adb(new string[] { "exec-out", "screencap", "-p" }, device);
            using FileStream f = File.OpenWrite(filename);
            f.Write(r);
            return basefilename;
        }
        public string cut_image(string name, int x1, int y1, int x2, int y2)
        {
            var img = LoadImage(name);
            if (img == null || img.Height <= 0 || img.Empty())
            {
                return "Error";
            }
            Cv2.ImWrite($"{data_path}/out_cutiamge.png", img.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)));
            var gray = new Mat(y2 - y1, x2 - x1, MatType.CV_8U);
            Cv2.CvtColor(img.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), gray, ColorConversionCodes.RGB2GRAY);
            const int threshold = 80;
            Cv2.Threshold(gray, gray, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.ImWrite($"{data_path}/out_cutiamge_gray.png", gray);

            return "out_cutiamge.png";
        }
        public Mat? LoadImage(string name)
        {
            if (!img_caches.ContainsKey(name))
            {
                try
                {
                    img_caches[name] = Cv2.ImRead($"{data_path}/match/{name}");
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                    return null;
                }
            }
            return img_caches[name];
        }
        public bool MatchImage(string str, Mat img1, Mat img2, int x1, int y1, int x2, int y2)
        {
            try
            {
                if (img1.Empty())
                {
                    return false;
                }
                var height = img1.Height;
                var width = img1.Width;
                OpenCvSharp.Size image_size = new(Math.Max(height, 256), Math.Max(width, 256));
                var gray1 = new Mat(y2 - y1, x2 - x1, MatType.CV_8U);
                var gray2 = new Mat(y2 - y1, x2 - x1, MatType.CV_8U);
                Cv2.CvtColor(img2.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), gray2, ColorConversionCodes.RGB2GRAY);
                const int threshold = 80;
                Cv2.Threshold(gray2, gray2, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                Cv2.Resize(gray2, gray2, image_size);

                Mat target_des = new();
                Mat comparing_des = new();
                try
                {
                    if (match_img_caches.ContainsKey(str))
                    {
                        target_des = match_img_caches[str];
                    }
                    else
                    {
                        Cv2.CvtColor(img1.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), gray1, ColorConversionCodes.RGB2GRAY);
                        Cv2.Threshold(gray1, gray1, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                        Cv2.Resize(gray1, gray1, image_size);

                        detector.DetectAndCompute(gray1, null, out _, target_des);
                        match_img_caches[str] = target_des;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                    return false;
                }
                detector.DetectAndCompute(gray2, null, out _, comparing_des);
                if (comparing_des.Empty())
                {
                    return false;
                }
                DMatch[] matches;
                try
                {
                    matches = bf.Match(target_des, comparing_des);
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                    return false;
                }
                // 特徴量の距離を出し、平均を取る
                float ret = 200;
                if (matches.Length > 0)
                {
                    ret = matches.Select(item => item.Distance).Average();
                }
                if (width > 100)
                {
                    return ret < 50;
                }
                return ret < 40;
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
                return false;
            }
        }
        public double MatchImage2(string str1, string str2, Mat img1, Mat img2, int x1, int y1, int x2, int y2)
        {
            try
            {
                if (img1.Empty())
                {
                    return -1;
                }
                str1 = $"{str1}_{x1}.{y1}.{x2}.{y2}";
                str2 = $"{str2}_{x1}.{y1}.{x2}.{y2}";

                try
                {
                    if (!match_img_hash_caches.ContainsKey(str1))
                    {
                        var outarray = new Mat();
                        hash_func.Compute(img1.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), outarray);
                        match_img_hash_caches[str1] = outarray;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                }
                try
                {
                    if (!match_img_hash_caches.ContainsKey(str2))
                    {
                        var outarray = new Mat();
                        hash_func.Compute(img2.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), outarray);
                        match_img_hash_caches[str2] = outarray;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                }

                var r = hash_func.Compare(match_img_hash_caches[str1], match_img_hash_caches[str2]);
                return r;
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
                return -1;
            }
        }
        public string matchTemplate(string name)
        {
            try
            {
                Mat? img_screen = capture_screen();
                if (img_screen == null || img_screen.Empty())
                {
                    return "Error";
                }
                Cv2.CvtColor(img_screen, img_screen, ColorConversionCodes.RGB2GRAY);
                var template = LoadImage(name);
                if (template == null)
                {
                    return "Error";
                }
                Cv2.CvtColor(template, template, ColorConversionCodes.RGB2GRAY);

                var res = new Mat();
                Cv2.MatchTemplate(img_screen, template, res, TemplateMatchModes.CCoeffNormed);
                const double threshold = 0.9;
                OpenCvSharp.Point minloc, maxloc;
                double minval, maxval;
                Cv2.MinMaxLoc(res, out minval, out maxval, out minloc, out maxloc);
                if (maxval >= threshold)
                {
                    return $"{maxloc.X},{maxloc.Y}";
                }
                return "None";
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
                return "Error";
            }
        }

        // ターゲット
        public string HasPvPTarget(int x1, int y1, int x2, int y2)
        {
            try
            {
                var img_screen = capture_screen();
                if (img_screen == null || img_screen.Empty())
                {
                    return "Error";
                }
                var radar_mask_org = LoadImage("radar_mask.png");
                if (radar_mask_org == null || radar_mask_org.Empty())
                {
                    return "Error";
                }
                string move_ret = "Wait";
                Mat img = new();
                Cv2.CvtColor(img_screen, img, ColorConversionCodes.BGR2GRAY);
                Mat radar_mask = new();
                Cv2.CvtColor(radar_mask_org, radar_mask, ColorConversionCodes.BGR2GRAY);
                const int threshold = 150;
                Cv2.Threshold(img, img, threshold, 255, ThresholdTypes.Binary);
                Cv2.Threshold(radar_mask, radar_mask, threshold, 255, ThresholdTypes.Binary);
                Mat radar_img = new();
                Cv2.BitwiseAnd(img, radar_mask, radar_img);

                if (match_img_caches.ContainsKey("@MoveCheck"))
                {
                    var pre_image = match_img_caches["@MoveCheck"];
                    Mat im_diff = new();
                    Cv2.Absdiff(pre_image, radar_img, im_diff);
                    double maxVal;
                    Cv2.MinMaxIdx(im_diff, out _, out maxVal);
                    if (maxVal < 100)
                    {
                        move_ret = "Wait";
                        // print("MoveCheck : Wait %d<100?" % (im_diff.max()))
                    }
                    else
                    {
                        move_ret = "Move";
                        // print("MoveCheck : Move %d<100?" % (im_diff.max()))
                    }
                }
                match_img_caches["@MoveCheck"] = radar_img;
                var perf_counter = DateTime.Now.Subtract(new DateTime(2022, 1, 1, 0, 0, 0)).TotalMilliseconds;
                // １秒以内なら変更しない
                if (pvp_result != "Wait" && perf_counter - wait_check_time < 1000)
                {
                    move_ret = "Move";
                }
                else if (pvp_result == "Wait" && perf_counter - wait_check_time < 1000)
                {
                    move_ret = "Wait";
                }
                if (move_ret != pvp_result)
                {
                    wait_check_time = perf_counter;
                }
                Mat frame_mask1 = new();
                // BGR色空間からHSV色空間への変換
                Mat hsv = new();
                Cv2.CvtColor(img_screen, hsv, ColorConversionCodes.BGR2HSV);
                {
                    // 色検出しきい値の設定
                    int[] lower = { 150, 180, 0 };
                    int[] upper = { 180, 255, 255 };
                    // 色検出しきい値範囲内の色を抽出するマスクを作成
                    Cv2.InRange(hsv
                        , new Mat(lower.Length, 1, MatType.CV_32SC1, data: lower)
                        , new Mat(upper.Length, 1, MatType.CV_32SC1, data: upper)
                        , frame_mask1);
                }

                OpenCvSharp.Point[][] contours;
                Cv2.FindContours(frame_mask1, out contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxTC89L1);
                foreach (var contour in contours)
                {
                    // 輪郭の周囲に比例する精度で輪郭を近似する
                    var arclen = Cv2.ArcLength(contour, true);
                    var approx = Cv2.ApproxPolyDP(contour, arclen * 0.02, true);
                    // 四角形の輪郭は、近似後に4つの頂点があります
                    // 比較的広い領域が凸状になります。
                    // 凸性の確認 
                    var area = Cv2.ContourArea(approx);
                    if (approx.Length == 4 && area > 100 && Cv2.IsContourConvex(approx))
                    {
                        int width = Math.Abs(approx[2].X - approx[0].X);
                        int height = Math.Abs(approx[2].Y - approx[0].Y);
                        // print("HasPvPTarget:%d,%d" % (width, height))
                        if (approx[0].X > 1000 && approx[0].Y > 300)
                        {
                            //#print("HasPvPTarget:%d,%d" % (approx[0][0][0], approx[0][0][1]))
                            continue;
                        }
                        else if (approx[0].X < 300 && approx[0].Y > 300)
                        {
                            //#print("HasPvPTarget:%d,%d" % (approx[0][0][0], approx[0][0][1]))
                            continue;
                        }
                        else if (width > height)
                        {
                            pvp_result = "Enemy";
                            // print("HasPvPTarget:Enemy (%d,%d)" % (approx[0][0][0], approx[0][0][1]))
                            return "Enemy";
                        }
                    }
                }
                // 占領
                // 占領地判定
                // 色検出しきい値の設定
                {
                    int[] lower = { 0, 0, 100 };
                    int[] upper = { 180, 45, 255 };
                    // 色検出しきい値範囲内の色を抽出するマスクを作成
                    Cv2.InRange(hsv
                        , new Mat(lower.Length, 1, MatType.CV_32SC1, data: lower)
                        , new Mat(upper.Length, 1, MatType.CV_32SC1, data: upper)
                        , frame_mask1);
                }
                // 外枠
                var mask_image1 = LoadImage("pvp_mask1.png");
                if (mask_image1 == null)
                {
                    return "Error";
                }
                Mat check_image1 = new();
                Cv2.BitwiseAnd(hsv, mask_image1, check_image1);
                Mat result_image1 = new();
                {
                    int[] lower = { 0, 0, 100 };
                    int[] upper = { 180, 45, 255 };
                    // 色検出しきい値範囲内の色を抽出するマスクを作成
                    Cv2.InRange(check_image1
                        , new Mat(lower.Length, 1, MatType.CV_32SC1, data: lower)
                        , new Mat(upper.Length, 1, MatType.CV_32SC1, data: upper)
                        , result_image1);
                }
                if (Cv2.CountNonZero(result_image1) > 500)
                {
                    // 中央（模様）
                    var mask_image2 = LoadImage("pvp_mask2.png");
                    if (mask_image2 == null)
                    {
                        return "Error";
                    }
                    Mat result_image2 = new();
                    Mat check_image2 = new();
                    Cv2.BitwiseAnd(hsv, mask_image2, check_image2);
                    {
                        int[] lower = { 0, 0, 100 };
                        int[] upper = { 180, 45, 255 };
                        // 色検出しきい値範囲内の色を抽出するマスクを作成
                        Cv2.InRange(check_image2
                            , new Mat(lower.Length, 1, MatType.CV_32SC1, data: lower)
                            , new Mat(upper.Length, 1, MatType.CV_32SC1, data: upper)
                            , result_image2);
                    }
                    if (Cv2.CountNonZero(result_image2) > 200)
                    {
                        // 枠内
                        var mask_image3 = LoadImage("pvp_mask3.png");
                        if (mask_image3 == null)
                        {
                            return "Error";
                        }
                        Mat target_mask1 = new();
                        Mat check_image3 = new();
                        Cv2.BitwiseAnd(hsv, mask_image3, check_image3);
                        {
                            int[] lower = { 90, 120, 100 };
                            int[] upper = { 150, 255, 255 };
                            // 水色 色検出しきい値の設定
                            Cv2.InRange(check_image3
                                , new Mat(lower.Length, 1, MatType.CV_32SC1, data: lower)
                                , new Mat(upper.Length, 1, MatType.CV_32SC1, data: upper)
                                , target_mask1);
                        }
                        if (Cv2.CountNonZero(target_mask1) > 200)
                        {
                            // 未占領
                            // print("HasPvPTarget:Place")
                            pvp_result = "Place";
                            return "Place";
                        }
                    }
                }
                pvp_result = move_ret;
                // print("HasPvPTarget:%s" % (self.pvp_result))
                return pvp_result;
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
                return "Error";
            }
        }

        public void set_device(string n)
        {
            current_device = n;
        }
        public Dictionary<string, string> jump()
        {
            exec_adb(new string[] { "shell", "input", "swipe", "206", "624", "134", "555" });
            exec_adb(new string[] { "shell", "input", "swipe", "134", "555", "206", "480" });
            exec_adb(new string[] { "shell", "input", "swipe", "206", "480", "275", "555" });
            exec_adb(new string[] { "shell", "input", "swipe", "275", "555", "206", "624" });
            return new() { ["message"] = "OK" };
        }
        public void Sleep(double secs)
        {
            Thread.Sleep((int)(secs * 1000));
        }
        public Dictionary<string, string> Swipe(int x1, int y1, int x2, int y2, string device = "")
        {
            exec_adb(new string[] { "shell", "input", "swipe", $"{x1}", $"{y1}", $"{x2}", $"{y2}" }, device);
            return new() { ["message"] = "OK" };
        }
        public Dictionary<string, string> Touch(int x, int y, string device = "")
        {
            exec_adb(new string[] { "shell", "input", "touchscreen", "tap", $"{x}", $"{y}" }, device);
            return new() { ["message"] = "OK" };
        }
        public Dictionary<string, string> KeyDown(int code, string device = "")
        {
            exec_adb(new string[] { "shell", $"sendevent /dev/input/event4 1 {code} 1 ; sendevent /dev/input/event4 0 0 0" }, device);
            return new() { ["message"] = "OK" };
        }
        public Dictionary<string, string> KeyUp(int code, string device = "")
        {
            exec_adb(new string[] { "shell", $"sendevent /dev/input/event4 1 {code} 0 ; sendevent /dev/input/event4 0 0 0" }, device);
            return new() { ["message"] = "OK" };
        }
        public Dictionary<string, string> InputText(string text, string device = "")
        {
            exec_adb(new string[] { "shell", "input", "text", text }, device);
            return new() { ["message"] = "OK" };
        }
        public int MatchImageList(object[][] match_list)
        {
            try
            {
                var img_screen = capture_screen();
                if (img_screen == null || img_screen.Empty())
                {
                    return -1;
                }
                int index = -1;
                foreach (var match in match_list)
                {
                    ++index;
                    if (match.Length < 5)
                    {
                        continue;
                    }
                    string str = Convert.ToString(match[0] ?? "") ?? "";
                    string img = Convert.ToString(match[1] ?? "") ?? "";
                    int x1 = Convert.ToInt32(match[2] ?? 0);
                    int y1 = Convert.ToInt32(match[3] ?? 0);
                    int x2 = Convert.ToInt32(match[4] ?? 0);
                    int y2 = Convert.ToInt32(match[5] ?? 0);

                    if (MatchImage(str, LoadImage(img)!, img_screen, x1, y1, x2, y2))
                    {
                        return index;
                    }
                }

            }
            catch (Exception ex)
            {
                ErrorLog(ex);
            }
            return -1;
        }
        public float MatchImageTest(string str, int x1, int y1, int x2, int y2)
        {
            try
            {
                var img1 = LoadImage(str);
                if (img1 == null || img1.Empty())
                {
                    return -1;
                }
                var img2 = capture_screen();
                if (img2 == null || img2.Empty())
                {
                    return -1;
                }
                var height = img1.Height;
                var width = img1.Width;
                OpenCvSharp.Size image_size = new(Math.Max(height, 256), Math.Max(width, 256));
                var gray1 = new Mat(y2 - y1, x2 - x1, MatType.CV_8U);
                var gray2 = new Mat(y2 - y1, x2 - x1, MatType.CV_8U);
                Cv2.CvtColor(img2.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), gray2, ColorConversionCodes.RGB2GRAY);
                const int threshold = 80;
                Cv2.Threshold(gray2, gray2, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                Cv2.Resize(gray2, gray2, image_size);

                Mat target_des = new();
                Mat comparing_des = new();
                try
                {
                    Cv2.CvtColor(img1.Clone(new Rect(x1, y1, x2 - x1, y2 - y1)), gray1, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(gray1, gray1, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                    Cv2.Resize(gray1, gray1, image_size);

                    detector.DetectAndCompute(gray1, null, out _, target_des);
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                    return -1;
                }
                detector.DetectAndCompute(gray2, null, out _, comparing_des);
                if (comparing_des.Empty())
                {
                    return -1;
                }
                DMatch[] matches;
                try
                {
                    matches = bf.Match(target_des, comparing_des);
                }
                catch (Exception ex)
                {
                    ErrorLog(ex);
                    return -1;
                }
                // 特徴量の距離を出し、平均を取る
                float ret = 200;
                if (matches.Length > 0)
                {
                    ret = matches.Select(item => item.Distance).Average();
                }
                return ret;
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
                return -1;
            }
        }
        public void deleteMatchCache(Dictionary<string, Mat> l, string m)
        {
            List<string> keys = new();
            foreach (var item in l)
            {
                if (item.Key.Contains(m))
                {
                    keys.Add(item.Key);
                }
            }
            foreach (var key in keys)
            {
                l.Remove(key);
            }
        }
        public Dictionary<string, object> MatchAndTouchEx(object[][] match_list)
        {
            Dictionary<string, object> ret = new()
            {
                ["message"] = "",
                ["index"] = -1
            };
            try
            {
                var img_screen = capture_screen();
                if (img_screen == null || img_screen.Empty())
                {
                    return ret;
                }
                int cnt = 0;
                foreach (object[] match in match_list)
                {
                    ++cnt;
                    if (match.Length < 6)
                    {
                        continue;
                    }
                    string str = Convert.ToString(match[0] ?? "") ?? "";
                    if (!img_caches.ContainsKey(str))
                    {
                        LoadImage(str);
                        print_line($"cached:{cnt}/{match_list.Length}:{str}");
                    }
                }
                //
                deleteMatchCache(match_img_caches, "@MatchAndTouchEx");
                deleteMatchCache(match_img_hash_caches, "@MatchAndTouchEx");
                //
                int index = -1;
                foreach (var match in match_list)
                {
                    ++index;
                    if (match.Length < 6)
                    {
                        continue;
                    }
                    string str = Convert.ToString(match[0] ?? "") ?? "";
                    string img = Convert.ToString(match[1] ?? "") ?? "";
                    int x1 = Convert.ToInt32(match[2] ?? 0);
                    int y1 = Convert.ToInt32(match[3] ?? 0);
                    int x2 = Convert.ToInt32(match[4] ?? 0);
                    int y2 = Convert.ToInt32(match[5] ?? 0);

                    var img_src = LoadImage(img);
                    if (img_src == null || img_src.Empty())
                    {
                        continue;
                    }
                    var match_result = MatchImage2(str, "@MatchAndTouchEx_img_screen", img_src, img_screen, x1, y1, x2, y2);
                    if (match_result >= 0.9995)
                    {
                        ErrorLog($"{img}:{match_result}");
                        object[] cur_match = match_list[index];
                        if(cur_match == null)
                        {
                            ErrorLog($"{img}:{match_result}");
                            continue;
                        }
                        List<object> src_rect_obj = (List<object>)cur_match[6];
                        if (src_rect_obj == null)
                        {
                            ErrorLog($"{img}:{match_result}");
                            continue;
                        }
                        int[] src_rect = new int[]
                        {
                            Convert.ToInt32(src_rect_obj[0] ?? 0),
                            Convert.ToInt32(src_rect_obj[1] ?? 0),
                            Convert.ToInt32(src_rect_obj[2] ?? 0),
                            Convert.ToInt32(src_rect_obj[3] ?? 0),
                        };
                        List<object> dest_pos_list = (List<object>)cur_match[7];
                        if (dest_pos_list == null)
                        {
                            ErrorLog($"{img}:{match_result}");
                            continue;
                        }
                        // ターゲットのボタン
                        var img_ans = img_src.Clone(new Rect(src_rect[0], src_rect[1], src_rect[2] - src_rect[0], src_rect[3] - src_rect[1]));
                        int w = src_rect[2] - src_rect[0];
                        int h = src_rect[3] - src_rect[1];
                        double r = -1;
                        int tx = 0;
                        int ty = 0;
                        // ボタン位置を検索
                        foreach (var item_obj in dest_pos_list)
                        {
                            List<object> item = (List<object>)item_obj;
                            if (item.Count < 2)
                            {
                                continue;
                            }
                            int dx = Convert.ToInt32(item[0] ?? 0);
                            int dy = Convert.ToInt32(item[1] ?? 0);
                            var img_target = img_screen.Clone(new Rect(dx, dy, w, h));
                            deleteMatchCache(match_img_caches, "@MatchAndTouchEx_img_target");
                            deleteMatchCache(match_img_hash_caches, "@MatchAndTouchEx_img_target");
                            var r0 = MatchImage2("@MatchAndTouchEx_img_ans", "@MatchAndTouchEx_img_target", img_ans, img_target, 0, 0, w, h);
                            if (r0 >= 0.9995 && (r == -1 || r0 > r))
                            {
                                r = r0;
                                tx = dx;
                                ty = dy;
                            }
                        }
                        ErrorLog($"{img}:{r}");
                        if (r >= 0)
                        {
                            //print(str)
                            Touch(tx + w / 2, ty + h / 2);
                            ret["message"] = $"[{str}]";
                            ret["index"] = index;
                            return ret;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
            }
            return ret;
        }
        public Dictionary<string, object> MatchAndTouch(object[][] match_list)
        {
            Dictionary<string, object> ret = new()
            {
                ["message"] = "",
                ["index"] = -1
            };
            try
            {
                var i = MatchImageList(match_list);
                if (i >= 0)
                {
                    var item = match_list[i];
                    if (item == null || item.Length < 8)
                    {
                        return ret;
                    }
                    string str = Convert.ToString(item[0] ?? "") ?? "";
                    int tx = Convert.ToInt32(item[6] ?? 0);
                    int ty = Convert.ToInt32(item[7] ?? 0);
                    if (tx >= 0)
                    {
                        Touch(tx, ty);
                    }
                    return new()
                    {
                        ["message"] = $"[{str}]",
                        ["index"] = i
                    };

                }
            }
            catch (Exception ex)
            {
                ErrorLog(ex);
            }
            //  print("Not Match")
            return ret;
        }

        // デバイスリスト
        public Dictionary<string, string> device_list()
        {
            byte[] r = exec_adb(new string[] { "devices" });
            return new() { ["message"] = Encoding.GetEncoding("shift_jis").GetString(r) };
        }
        private void ErrorLog(Exception e, [CallerLineNumber] int line = 0, [CallerMemberName] string name = "", [CallerFilePath] string path = "")
        {
            if (AddLog != null)
            {
                AddLog($"Log:{name}:{line}\r\n{e.Message}\r\n{e.StackTrace}");
            }
        }
        private void ErrorLog(string mes, [CallerLineNumber] int line = 0, [CallerMemberName] string name = "", [CallerFilePath] string path = "")
        {
            if (AddLog != null)
            {
                AddLog($"Log:{name}:{line}\r\n{mes}");
            }
        }
        public void print_line(string mes, [CallerLineNumber] int line = 0, [CallerMemberName] string name = "", [CallerFilePath] string path = "")
        {
            if (AddLog != null)
            {
                AddLog($"Log:{name}:{line}:{mes}");
            }
            else if (AddMessage == null || !AddMessage.CanExecute)
            {
                Console.WriteLine(mes);
            }
            else
            {
                AddMessage.ExecuteAsync($"Log:{name}:{line}:{mes}");
            }
        }
    }
}
