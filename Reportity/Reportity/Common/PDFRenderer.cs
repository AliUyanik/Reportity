﻿using iTextSharp.text;
using iTextSharp.text.pdf;
using Reportity.Abstractions;
using Reportity.Core;
using Reportity.Exception;
using Reportity.Helper;
using Reportity.Utils;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

namespace Reportity.Common
{
    internal class PDFRenderer<T> : Renderer<T>, IStringExporter<T>, IByteExporter<T>
    {
        const int ceiling = 25;
        public byte[] ExportToStream(IEnumerable<T> list)
        {
            return RenderData(list);
        }

        public string ExportToString(IEnumerable<T> list)
        {
            return Convert.ToBase64String(RenderData(list));
        }

        public override byte[] RenderData(IEnumerable<T> list)
        {
            using (MemoryStream ReportData = new MemoryStream())
            {
                try
                {
                    using (ReportityReportObject ReportObject = new ReportityReportObject(typeof(T)))
                    {
                        ReportObject.setHeaders();
                        ReportObject.setAttributes();

                        PdfPTable? table = null;
                        int colCount = ReportObject.Cells.Count;
                        table = new PdfPTable(colCount);
                        table.HorizontalAlignment = 1;
                        table.WidthPercentage = 100;

                        BaseFont bf = BaseFont.CreateFont(
                                                        BaseFont.HELVETICA,
                                                        "CP1254",
                                                        BaseFont.NOT_EMBEDDED,
                                                        false);

                        int[] colWidths = new int[colCount];

                        PdfPCell cell;
                        string cellText;

                        float fontvalue = (ceiling - colCount) / 2;
                        if (fontvalue < 6)
                            fontvalue = 6;
                        else if (fontvalue > 15)
                            fontvalue = 15;
                        foreach (var item in ReportObject.Cells)
                        {
                            iTextSharp.text.Font font = new iTextSharp.text.Font(bf, fontvalue, iTextSharp.text.Font.NORMAL, BaseColor.White);
                            cell = new PdfPCell(new Phrase(item.ToString()?.Replace("<br />", Environment.NewLine), font));

                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            cell.FixedHeight = 55f;

                            cell.BackgroundColor = new BaseColor(System.Drawing.ColorTranslator.FromHtml("#a52a2a"));

                            table.AddCell(cell);
                        }
                        bool color = false;

                        foreach (var data in list)
                        {
                            foreach (PropertyInfo propertyInfo in data.GetType().GetProperties())
                            {
                                if (TypeChecker.CheckType(propertyInfo.PropertyType))
                                {
                                    ReportObject.takeSummaryObjects(propertyInfo, data);

                                    cellText = propertyInfo.GetValue(data)?.ToString();

                                    iTextSharp.text.Font font = new iTextSharp.text.Font(bf, fontvalue, iTextSharp.text.Font.NORMAL, BaseColor.Black);
                                    cell = new PdfPCell(new Phrase(cellText?.Replace("<br />", Environment.NewLine), font));

                                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                                    cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                                    cell.MinimumHeight = 35f;
                                    cell.BackgroundColor = new BaseColor(color ? Color.LightGray : Color.AliceBlue);

                                    table.AddCell(cell);
                                }
                            }
                            color = !color;
                        }

                        if (ReportObject.SummaryType != null)
                        {
                            decimal? summarytotal = ReportObject.SummaryValues.Sum();
                            iTextSharp.text.Font fontsummary = new iTextSharp.text.Font(bf, fontvalue, iTextSharp.text.Font.BOLDITALIC, BaseColor.Black);

                            for (int i = 0; i < ReportObject.Cells.Count - 2; i++)
                            {
                                cell = new PdfPCell(new Phrase(""));
                                cell.BackgroundColor = new BaseColor(Color.Gray);
                                table.AddCell(cell);
                            }

                            cell = new PdfPCell(new Phrase(("Toplam " + ReportObject.SummaryName).Replace("<br />", Environment.NewLine), fontsummary));

                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            cell.MinimumHeight = 35f;
                            cell.BackgroundColor = new BaseColor(Color.Gray);

                            table.AddCell(cell);

                            cell = new PdfPCell(new Phrase(summarytotal.ToString()?.Replace("<br />", Environment.NewLine), fontsummary));

                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            cell.MinimumHeight = 35f;
                            cell.BackgroundColor = new BaseColor(Color.Gray);

                            table.AddCell(cell);
                        }

                        Document pdfDoc = new Document(PageSize.A4);
                        if (ReportObject.Cells.Count > 7)
                            pdfDoc = new Document(PageSize.A4.Rotate());

                        PdfWriter.GetInstance(pdfDoc, ReportData);
                        pdfDoc.Open();
                        table.HeaderRows = 1;

                        iTextSharp.text.Font fheader = new iTextSharp.text.Font(bf, 15, iTextSharp.text.Font.BOLD, BaseColor.Black);
                        iTextSharp.text.Font fdate = new iTextSharp.text.Font(bf, 11, iTextSharp.text.Font.ITALIC, BaseColor.Black);

                        pdfDoc.Add(new Paragraph(DateTime.Now.ToString(), fdate) { Alignment = Element.ALIGN_RIGHT });
                        pdfDoc.Add(new Paragraph(ReportObject.ReportHeader, fheader) { Alignment = Element.ALIGN_CENTER });

                        if (ReportObject.LogoPath != "")
                        {
                            try
                            {
                                System.Drawing.Image imagefromfile = System.Drawing.Image.FromFile(ReportObject.LogoPath);
                                string[] extensionList = ReportObject.LogoPath.Split(".");
                                string extension = extensionList.Last().ToUpper();

                                iTextSharp.text.Image? image = null;

                                switch (extension)
                                {
                                    case "PNG":
                                        image = iTextSharp.text.Image.GetInstance(imagefromfile, ImageFormat.Png);
                                        break;
                                    case "JPG":
                                        image = iTextSharp.text.Image.GetInstance(imagefromfile, ImageFormat.Jpeg);
                                        break;
                                }

                                image?.SetDpi(100, 100);
                                image?.SetAbsolutePosition(20, pdfDoc.Top - 40);
                                image?.ScaleToFit(100, 100);

                                pdfDoc.Add(image);
                            }
                            catch (System.Exception ex)
                            {
                                throw new ReportitiyException(ex.Message);
                            }
                        }

                        pdfDoc.Add(new Paragraph(" "));
                        pdfDoc.Add(table);
                        pdfDoc.Close();
                    }
                }
                catch (System.Exception ex)
                {
                    throw new ReportitiyException(ex.Message);
                }
                return ReportData.ToArray();
            }
        }
    }
}
