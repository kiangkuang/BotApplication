using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.CPlusPlus;

namespace Bot_Application.Controllers
{
    public class OpenCv
    {
        public IEnumerable<IEnumerable<Mat>> ProcessImage(string file)
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

            return SplitImages(cleaned);
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

        private Point[] GetContour(Mat edged)
        {
            Point[][] contours;
            HierarchyIndex[] hierarchyIndexes;
            Cv2.FindContours(edged.Clone(), out contours, out hierarchyIndexes, ContourRetrieval.List, ContourChain.ApproxSimple);
            var area = edged.Height * edged.Width;
            var sortedContours = contours.Where(c => GetContourArea(c) / area > 0.3).OrderByDescending(GetContourArea);

            foreach (var contour in sortedContours)
            {
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                if (approx.Length == 4)
                {
                    return approx;
                }
            }
            return null;
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

        private Mat FixPerspective(Mat orig, IList<Point> screenContour, Rect boundRect, float ratio)
        {
            var transMtx = GetTransMtx(screenContour, boundRect, ratio);
            var transformed = new Mat();
            Cv2.WarpPerspective(orig, transformed, transMtx, orig.Size());
            return transformed;
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

        private static Mat CleanImage(Mat cropped)
        {
            var grey = cropped.CvtColor(ColorConversion.RgbToGray);
            return grey.Threshold(127, 255, ThresholdType.Binary);
        }

        private IEnumerable<IEnumerable<Mat>> SplitImages(Mat image)
        {
            var dilate = new Mat();
            var element = Cv2.GetStructuringElement(StructuringElementShape.Rect, new Size(image.Width / 40, 1));
            Cv2.Dilate(image.Threshold(127, 255, ThresholdType.BinaryInv), dilate, element);

            HierarchyIndex[] hierarchyIndices;
            Point[][] contours;
            Cv2.FindContours(dilate, out contours, out hierarchyIndices, ContourRetrieval.External, ContourChain.ApproxNone);

            var contourRows = SortToRows(contours);

            var imageRows = contourRows.Select(r => r.Select(c =>
            {
                var img = new Mat();
                Cv2.CopyMakeBorder(img[Cv2.BoundingRect(c)], img, 10, 10, 10, 10, BorderType.Constant, Scalar.White);

                // using (new Window("image", segment)) { Cv2.WaitKey(); }

                return img;
            }));

            return imageRows;
        }

        private IEnumerable<IEnumerable<Point[]>> SortToRows(IEnumerable<Point[]> contours)
        {
            var contourRows = new List<List<Point[]>>();
            foreach (var contour in contours.OrderBy(c => c[0].Y))
            {
                if (IsSameRow(contourRows.DefaultIfEmpty(null).Last(), contour))
                {
                    contourRows.Last().Add(contour);
                }
                else
                {
                    contourRows.Add(new List<Point[]> { contour });
                }
            }

            return contourRows.Select(r => r.OrderBy(c => c[0].X));
        }

        private static bool IsSameRow(IEnumerable<Point[]> contourRow, Point[] contour)
        {
            return contourRow != null && Math.Abs(contour[0].Y - contourRow.Average(c => c[0].Y)) < 10;
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

        private Rect ResizeBoundRect(Rect boundRect, float ratio)
        {
            return new Rect(
                (int)(boundRect.X / ratio),
                (int)(boundRect.Y / ratio),
                (int)(boundRect.Width / ratio),
                (int)(boundRect.Height / ratio));
        }
    }
}