using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SLT_SeatBooking.Models;
using System;
using System.ComponentModel;
using System.Net;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Runtime.InteropServices.JavaScript.JSType;
using QRCoder;
using System.Drawing;
using System.IO;
using Azure.Core;
using System.Drawing.Imaging;
using Microsoft.AspNetCore.Http.HttpResults;
using ZXing;
using ZXing.QrCode;
using ZXing.OneD;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json.Linq;
using ThirdParty.Json.LitJson;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Text;
using System.Linq.Expressions;
using Org.BouncyCastle.Tls;


namespace SLT_SeatBooking.Controllers
{
    public class BookController : Controller
    {
		private readonly string _connectionString;

		public BookController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
		[Route ("/BookNow")]
		public IActionResult BookNow()
        {
            //var bookings = GetBookings();
            return View();
        }

        public IActionResult ConfirmBooking() {
           
			return View(); 
        
        }

		public IActionResult MyBookings()
		{
			return View();
		}

		[Route("/ConfirmBooking")]
		public IActionResult ConfirmBookings()
		{
			return View();
		}
		public List<Bookings> GetBookings()
        {
            string query = "SELECT * FROM Booking";
            
			var bookings = new List<Bookings>();

            try
            {

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookings.Add(new Bookings
                                {

                                    BookId = reader["BookId"] != DBNull.Value ? (int)reader["BookId"] : 0, // Defaulting to 0 if null
                                    TraineeID = reader["TraineeName"]?.ToString() ?? string.Empty, // Handles potential null values
                                    TraineeName = reader["TraineeName"]?.ToString() ?? string.Empty, // Handles potential null values                                                                      //BookDateTime = reader[],
                                    SeatNumber = reader["SeatNumber"]?.ToString() ?? string.Empty,
                                    TraineeNIC = reader["TraineeNIC"]?.ToString() ?? string.Empty

                                });
                            }
                        }


                    }
                }
				Console.Write("Success");
			} catch {
                Console.Write("Error");
            }
            return bookings;
		}

		[HttpGet]
		public async Task<IActionResult> GetSeatNumbers(string date)
		{
			var bookings = new List<Bookings>();
			string query = "SELECT SeatNumber FROM Booking WHERE BookDateTime = @date";

			using (SqlConnection conn = new SqlConnection(_connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand(query, conn))
				{
					command.Parameters.AddWithValue("@date", date);
					using (SqlDataReader reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							bookings.Add(new Bookings
							{

							SeatNumber = reader["SeatNumber"]?.ToString() ?? string.Empty,

							});


						}
					}

				}
			}

			return Json(bookings);
		}

		[HttpPost]
		public async Task<IActionResult> AddRequest([FromBody] Bookings data)
		{
			
			string query = "INSERT INTO Booking VALUES (@TraineeId,@TraineeName,@BookDateTime,@TraineeNIC,@SeatNumber)";

			if (data == null)
			{
				return BadRequest("Invalid data.");
			}
			try
			{
				using (SqlConnection conn = new SqlConnection(_connectionString))
				{
					conn.Open();
					using (SqlCommand command = new SqlCommand(query, conn))
					{
						command.Parameters.AddWithValue("@TraineeId", data.TraineeID);
						command.Parameters.AddWithValue("@TraineeName", data.TraineeName);
						command.Parameters.AddWithValue("@BookDateTime", data.BookDateTime);
						command.Parameters.AddWithValue("@TraineeNIC", data.TraineeNIC);
						command.Parameters.AddWithValue("@SeatNumber", data.SeatNumber);

						int result = command.ExecuteNonQuery();  // Execute the insert command

						// Check if the insertion was successful
						if (result > 0)
						{
							// Fetch the newly added records
							string selectQuery = "SELECT * FROM Booking WHERE SeatNumber = @SeatNumber";  // Modify this query as needed
							using (SqlCommand selectCommand = new SqlCommand(selectQuery, conn))
							{
								selectCommand.Parameters.AddWithValue("@SeatNumber", data.SeatNumber);  // Use the same parameter as above or adjust according to your logic

								using (SqlDataReader reader = selectCommand.ExecuteReader())
								{
									var booking = new Bookings();  // Assuming Booking is your model class
									while (reader.Read())
									{
										// Populate the bookings list with the data from the database

										booking.BookId = (int)reader["BookId"];
										booking.BookDateTime = (DateTime)reader["BookDateTime"];
										booking.TraineeID = reader["TraineeID"].ToString();
										booking.SeatNumber = reader["SeatNumber"].ToString();

									}

									// Return the bookings variable as part of the response
									return Json(booking);
								}

							}
						}
						else
						{
							return Json(new { message = "Failed to insert data.", success = false });
						}

					}
				}
			}catch(Exception ex)
			{
				return StatusCode(500, $"Internal server error: {ex.Message}");
			}

	}

		[HttpGet]
		public async Task<IActionResult> GetBookingsByDate(string date,string traineeid)
		{
			var bookings = new List<Bookings>();
			//traineeid = "3";
			string query = "SELECT * FROM Booking WHERE BookDateTime = @date AND TraineeId = @traineeid";

			if (string.IsNullOrEmpty(date))
			{
				Console.Write("Missing");
			}

			using (SqlConnection conn = new SqlConnection(_connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand(query, conn))
				{
					command.Parameters.AddWithValue("@date", date);
					command.Parameters.AddWithValue("@traineeid", traineeid);
					using (SqlDataReader reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							bookings.Add(new Bookings
							{
								BookId = (int)reader["BookId"],
								SeatNumber = reader["SeatNumber"]?.ToString() ?? string.Empty,
								BookDateTime = (DateTime)reader["BookDateTime"]

							});

						}
						reader.Close();
					}

				}
			}
			return Json(bookings);
		}

		[HttpPost]
		public IActionResult QRGen([FromBody] Bookings bookings)
		{
			string qrContent = null;
			try
			{
				// Convert leaveDetails to a string format (e.g., JSON) that can be encoded in the QR code
				qrContent = $"ID:{bookings.BookId}," +
							$"Seat Number: {bookings.SeatNumber}, " +
							$"Date:{bookings.BookDateTime},";					

				// Create a barcode writer instance
				var barcodeWriter = new BarcodeWriterPixelData
				{
					Format = BarcodeFormat.QR_CODE,
					Options = new QrCodeEncodingOptions
					{
						Height = 200,
						Width = 200,
						Margin = 1
					}
				};

				// Generate the QR code
				var pixelData = barcodeWriter.Write(qrContent);

				// Create a Bitmap from the pixel data
				using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb))
				{
					using (var ms = new MemoryStream())
					{
						var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
						try
						{
							System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
						}
						finally
						{
							bitmap.UnlockBits(bitmapData);
						}

						// Save the bitmap to a memory stream as PNG
						bitmap.Save(ms, ImageFormat.Png);
						var byteImage = ms.ToArray();

						// Set a file name for the download
						var fileName = "QRCode.png";

						// Return the file as a downloadable attachment
						return File(byteImage, "image/png", fileName);
					}
				}
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = "Not working", error = ex.Message });
			}


		}

		[HttpPost]
		public IActionResult PDFGen([FromBody] Bookings bookings)
		{
			try
			{

			// Convert leaveDetails to a string format (e.g., JSON) that can be encoded in the QR code
			var pdfContent = $"ID:{bookings.BookId}," +
						$"Seat Number: {bookings.SeatNumber}, " +
						$"Date:{bookings.BookDateTime},";

			// Convert the booking object to a key-value string
			string content = ConvertToKeyValueString(bookings);

				// Create a memory stream to hold the PDF
				using (MemoryStream ms = new MemoryStream())
				{
					// Create a new document
					Document document = new Document();

					// Create a PDF writer linked to the memory stream
					PdfWriter.GetInstance(document, ms);

					// Open the document
					document.Open();

					// Add a title to the PDF
					document.Add(new Paragraph("Booking Details"));

					// Add the booking content to the PDF
					document.Add(new Paragraph(content));

					// Close the document
					document.Close();

					// Return the PDF as a downloadable file
					var byteArray = ms.ToArray();
					return File(byteArray, "application/pdf", "BookingDetails.pdf");
				}
			}catch(Exception ex)
			{
				return BadRequest(ex);
			}
		}
		private string ConvertToKeyValueString(object obj)
		{
			var properties = obj.GetType().GetProperties();
			var sb = new StringBuilder();

			foreach (var prop in properties)
			{
				var value = prop.GetValue(obj);
				sb.Append($"{prop.Name}:{value}, ");
			}

			// Remove the trailing comma and space
			if (sb.Length > 2)
				sb.Length -= 2;

			return sb.ToString();
		}
	}
}
