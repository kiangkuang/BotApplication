using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.CPlusPlus;

namespace Bot_Application.Controllers
{
    public class OpenCv
    {
        public string ProcessImage(string file)
        {
            var orig = Cv2.ImRead(file);
            var ratio = 1000f / Math.Max(orig.Height, orig.Width);
            var image = orig.Clone();

            var edged = CannyEdge(image, ratio);
            var contour = GetContour(edged);

            Mat cleaned;
            if (contour != null)
            {
                var boundRect = Cv2.BoundingRect(contour);
                var transformed = FixPerspective(orig, contour, boundRect, ratio);

                var cropped = transformed[ResizeBoundRect(boundRect, ratio)];
                cleaned = CleanImage(cropped);
            }
            else
            {
                cleaned = CleanImage(orig);
            }

            var segments = GetSegments(cleaned);

            /*
            using (new Window("img", final))
            {
                Cv2.WaitKey();
            }
            */

            // File.Delete(file);
            var newFile = file + "-final.jpg";
            Cv2.ImWrite(newFile, cleaned);

            return newFile;
        }

        private IEnumerable<Mat> GetSegments(Mat image)
        {
            var dilate = new Mat();
            var element = Cv2.GetStructuringElement(StructuringElementShape.Rect, new Size(image.Width / 40, 1));
            Cv2.Dilate(image.Threshold(127, 255, ThresholdType.BinaryInv), dilate, element);

            HierarchyIndex[] hierarchyIndices;
            Point[][] contours;
            Cv2.FindContours(dilate, out contours, out hierarchyIndices, ContourRetrieval.External, ContourChain.ApproxNone);

            var orderedContours = SortSegments(contours);

            var results = new List<Mat>();
            foreach (var contour in orderedContours)
            {
                var dst = new Mat();
                Cv2.CopyMakeBorder(image[Cv2.BoundingRect(contour)], dst, 10, 10, 10, 10, BorderType.Constant, Scalar.White);

                results.Add(dst);
            }
            return results;
        }

        private IEnumerable<Point[]> SortSegments(IEnumerable<Point[]> contours)
        {
            // TODO: group similar Y then order by X
            return contours.OrderBy(c => c[0].Y).ThenBy(c => c[0].X);
        }

        private Mat FixPerspective(Mat orig, IList<Point> screenContour, Rect boundRect, float ratio)
        {
            var transMtx = GetTransMtx(screenContour, boundRect, ratio);
            var transformed = new Mat();
            Cv2.WarpPerspective(orig, transformed, transMtx, orig.Size());
            return transformed;
        }

        private static Mat CleanImage(Mat cropped)
        {
            var grey = cropped.CvtColor(ColorConversion.RgbToGray);
            return grey.Threshold(127, 255, ThresholdType.Binary);
        }

        private Mat GetTransMtx(IList<Point> screenContour, Rect boundRect, float ratio)
        {
            var src = RatioPoints(ratio,
                screenContour[0].X, screenContour[0].Y,
                screenContour[1].X, screenContour[1].Y,
                screenContour[2].X, screenContour[2].Y,
                screenContour[3].X, screenContour[3].Y);
            var dest = RatioPoints(ratio,
                boundRect.Right, boundRect.Top,
                boundRect.Left, boundRect.Top,
                boundRect.Left, boundRect.Bottom,
                boundRect.Right, boundRect.Bottom);

            return Cv2.GetPerspectiveTransform(src, dest);
        }

        private Point[] GetContour(Mat edged)
        {
            Point[][] contours;
            HierarchyIndex[] hierarchyIndexes;
            Cv2.FindContours(edged.Clone(), out contours, out hierarchyIndexes, ContourRetrieval.List, ContourChain.ApproxSimple);
            var area = edged.Height * edged.Width;
            var sortedContours = contours.Where(c => GetContourArea(c) / area > 0.3).OrderByDescending(GetContourArea);

            Point[] selectedContour = null;
            foreach (var contour in sortedContours)
            {
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                if (approx.Length == 4)
                {
                    selectedContour = approx;
                    break;
                }
            }
            return selectedContour;
        }

        private Mat CannyEdge(Mat image, float ratio)
        {
            var blur = new Mat();
            var edge = new Mat();
            image = image.Resize(new Size(), ratio, ratio);
            Cv2.GaussianBlur(image, blur, new Size(5, 5), 0);
            Cv2.Canny(blur, edge, 75, 200);
            return edge;
        }

        private Rect ResizeBoundRect(Rect boundRect, float ratio)
        {
            return new Rect(
                (int)(boundRect.X / ratio),
                (int)(boundRect.Y / ratio),
                (int)(boundRect.Width / ratio),
                (int)(boundRect.Height / ratio));
        }

        private IEnumerable<Point2f> RatioPoints(float ratio, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4)
        {
            return new List<Point2f>
            {
                new Point2f(x1/ratio, y1/ratio),
                new Point2f(x2/ratio, y2/ratio),
                new Point2f(x3/ratio, y3/ratio),
                new Point2f(x4/ratio, y4/ratio)
            };
        }

        private double GetContourArea(Point[] polygon)
        {
            double area = 0;
            for (var i = 0; i < polygon.Length; i++)
            {
                var j = (i + 1) % polygon.Length;

                area += polygon[i].X * polygon[j].Y;
                area -= polygon[i].Y * polygon[j].X;
            }

            return Math.Abs(area / 2);
        }
    }
}