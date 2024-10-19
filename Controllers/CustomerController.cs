using Azure.Core;
using BKStore_MVC.Models;
using BKStore_MVC.Repository;
using BKStore_MVC.Repository.Interfaces;
using BKStore_MVC.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;
namespace BKStore_MVC.Controllers
{
	public class CustomerController : Controller
	{
		private readonly IBookRepository bookRepository;
		private readonly ICustomerRepository customerRepository;
		private readonly IGovernorateRepository governorateRepository;
		private readonly IOrderBookRepository orderBookRepository;
		private readonly IOrderRepository orderRepository;
		private readonly IShippingMethodRepository shippingMethodRepository;
		private readonly IShippingRepository shippingRepository;
		public CustomerController(IBookRepository bookRepository, IShippingMethodRepository shippingMethodRepository,
			ICustomerRepository customerRepository, IShippingRepository shippingRepository,
			IGovernorateRepository governorateRepository, IOrderBookRepository orderBookRepository,
			IOrderRepository orderRepository)
		{
			this.shippingMethodRepository = shippingMethodRepository;
			this.bookRepository = bookRepository;
			this.customerRepository = customerRepository;
			this.governorateRepository = governorateRepository;
			this.orderBookRepository = orderBookRepository;
			this.orderRepository = orderRepository;
			this.shippingRepository = shippingRepository;
		}
		public IActionResult AddCustomer(decimal TotalAmount)
		{
			ViewData["PaymentFees"] = shippingMethodRepository.GetByID(1).PaymentFees;

			// Retrieve the existing cookie
			var cookie = Request.Cookies["Cart"];
			List<BookCartItem> cartItems;

			if (cookie != null)
			{
				// Deserialize the existing cookie value
				cartItems = JsonConvert.DeserializeObject<List<BookCartItem>>(cookie) ?? new List<BookCartItem>();
			}
			else
			{
				// Initialize an empty list if the cookie does not exist
				cartItems = new List<BookCartItem>();
			}
			CustomerOrderVM customerOrderVM = new CustomerOrderVM
			{
				BookItems = cartItems,
				TotalAmount = TotalAmount
			};

			ViewData["Governoratelst"] = governorateRepository.GetAll();
			if (GetCustomerID() != "")
			{
				int CustomerID = int.Parse(GetCustomerID());
				customerOrderVM.Address = customerRepository.GetByID(CustomerID).Address;
				customerOrderVM.GovernorateID = customerRepository.GetByID(CustomerID).GovernorateID;
				customerOrderVM.Name = customerRepository.GetByID(CustomerID).Name;
				customerOrderVM.Nationalnumber = customerRepository.GetByID(CustomerID).Nationalnumber;
				customerOrderVM.Phone = customerRepository.GetByID(CustomerID).Phone;
				customerOrderVM.PaymentFees = shippingMethodRepository.GetByID(1).PaymentFees;
				return View("AddCustomer", customerOrderVM);
			}
			customerOrderVM.PaymentFees = shippingMethodRepository.GetByID(1).PaymentFees;
			return View("AddCustomer", customerOrderVM);
		}
		public IActionResult AddToCartBuy(int bookId, int Quantity)
		{
			ViewData["PaymentFees"] = shippingMethodRepository.GetByID(1).PaymentFees;

			var cookie = Request.Cookies["Cart"];
			List<BookCartItem> cartItems;

			if (cookie != null)
			{
				// Deserialize the existing cookie value
				cartItems = JsonConvert.DeserializeObject<List<BookCartItem>>(cookie);
			}
			else
			{
				// Initialize a new list if the cookie does not exist
				cartItems = new List<BookCartItem>();
			}
			Book book = bookRepository.GetByID(bookId);
			// Add the new item to the list
			if (book.StockQuantity < Quantity)
			{
				Quantity = book.StockQuantity;
			}
			else
			{
				cartItems.Add(new BookCartItem
				{
					BookId = bookId,
					Quantity = Quantity,
					ImagePath = book.ImagePath,
					Title = book.Title,
					Price = book.Price
				});
			}
			// Serialize the updated list
			string serializedCartItems = JsonConvert.SerializeObject(cartItems);

			// Create or update the cookie
			Response.Cookies.Append("Cart", serializedCartItems, new CookieOptions
			{
				Expires = DateTimeOffset.Now.AddDays(7) // Set the cookie to expire in 7 days
			});
			BookCartItem cartItem = new BookCartItem()
			{
				BookId = bookId,
				Quantity = Quantity,
				ImagePath = book.ImagePath,
				Title = book.Title,
				Price = book.Price

			};
			List<BookCartItem> BookCartItem = new List<BookCartItem>();
			BookCartItem.Add(item: cartItem);
			ViewData["Governoratelst"] = governorateRepository.GetAll();
			CustomerOrderVM customerOrderVM = new CustomerOrderVM
			{
				BookItems = BookCartItem,
				TotalAmount = (decimal?)(book.Price * Quantity + shippingMethodRepository.GetByID(1).PaymentFees)
			};
			if (GetCustomerID() != "")
			{
				int CustomerID = int.Parse(GetCustomerID().ToString());
				customerOrderVM.Address = customerRepository.GetByID(CustomerID).Address;
				customerOrderVM.GovernorateID = customerRepository.GetByID(CustomerID).GovernorateID;
				customerOrderVM.Name = customerRepository.GetByID(CustomerID).Name;
				customerOrderVM.Nationalnumber = customerRepository.GetByID(CustomerID).Nationalnumber;
				customerOrderVM.Phone = customerRepository.GetByID(CustomerID).Phone;
				customerOrderVM.PaymentFees = shippingMethodRepository.GetByID(1).PaymentFees;
				return View("AddCustomer", customerOrderVM);
			}
			customerOrderVM.PaymentFees = shippingMethodRepository.GetByID(1).PaymentFees;
			return View("AddCustomer", customerOrderVM);            //return RedirectToAction(nameof(ShowCart));

		}
        [HttpPost]
        public IActionResult SaveAdd(CustomerOrderVM customerOrderVM)
        {
            if (ModelState.IsValid)
            {
                if (customerOrderVM.Address != null)
                {
                    // Create a new Customer instance
                    Customer customer = new Customer
                    {
                        Name = customerOrderVM.Name,
                        Address = customerOrderVM.Address,
                        Phone = customerOrderVM.Phone,
                        GovernorateID = customerOrderVM.GovernorateID,
                        Nationalnumber = customerOrderVM.Nationalnumber,
                        // Do not set UserID to maintain uniqueness
                    };

                    // Add the new customer to the repository
                    customerRepository.Add(customer);
                    customerRepository.Save(); // Save the new customer

                    // Create the order
                    Order order = new Order
                    {
                        CustomerID = customer.ID, // Use the newly created customer ID
                        OrderDate = DateTime.Now,
                        DelivaryStatus = "Pending",
                        TotalAmount = (double?)customerOrderVM.TotalAmount
                    };
                    orderRepository.Add(order);
                    orderRepository.Save(); // Save the new order

                    // Create shipping information
                    Random random = new Random();
                    Shipping shipping = new Shipping()
                    {
                        ShippingMethodID = 1,
                        OrderID = order.OrderId,
                        ShippingDate = DateTime.Now,
                        TrackingNumber = random.Next(100000, 999999)
                    };
                    shippingRepository.Add(shipping);
                    shippingRepository.Save(); // Save shipping info

                    // Handle cart items
                    var cartCookie = Request.Cookies["Cart"];
                    List<BookCartItem> cartItems = !string.IsNullOrEmpty(cartCookie)
                        ? JsonConvert.DeserializeObject<List<BookCartItem>>(cartCookie) ?? new List<BookCartItem>()
                        : new List<BookCartItem>();

                    foreach (var item in cartItems)
                    {
                        Book bookedit = bookRepository.GetByID(item.BookId ?? 0);
                        if (bookedit != null)
                        {
                            bookedit.StockQuantity -= item.Quantity ?? 0;
                            OrderBook orderBook = new OrderBook
                            {
                                BookID = item.BookId ?? 0,
                                Quantity = item.Quantity ?? 0,
                                TSubPrice = (item.Price * item.Quantity) ?? 0,
                                OrderID = order.OrderId
                            };
                            bookRepository.Update(bookedit);
                            orderBookRepository.Add(orderBook);
                        }
                    }

                    // Save changes and delete the cart cookie
                    bookRepository.Save();
                    Response.Cookies.Delete("Cart");

                    TempData["OrderSuccessMessage"] = "Your order has been placed successfully!";
                    return RedirectToAction("Index", "Book");
                }
            }

            ViewData["Governoratelst"] = governorateRepository.GetAll();
            return View("AddCustomer", customerOrderVM);
        }


        public IActionResult Details(int ID)
		{
			int OrderId;
			int customerID = ID;

			if (customerID == 0)
			{
				return BadRequest("Customer ID cannot be null or empty.");
			}

			OrderId = orderRepository.GetByCustomerID(customerID).OrderId;

			List<OrderBook> orderBook = orderBookRepository.GetByID(OrderId);
			List<BookCartItem> bookCartItems = new List<BookCartItem>();
			OrderDetailVM orderDetailVM = new OrderDetailVM();

			if (orderBook != null)
			{
				foreach (var item in orderBook)
				{
					var book = bookRepository.GetByID(item.BookID ?? 0);
					BookCartItem bookCart = new BookCartItem
					{
						Title = book.Title,
						Quantity = item.Quantity,
						Price = book.Price,
						ImagePath = book.ImagePath,
						BookId = item.BookID
					};

					bookCartItems.Add(bookCart);

				}
				//orderDetailVM.bookCartItems = bookCartItems;
			}

			var order = orderRepository.GetByID(OrderId);
			var customer = customerRepository.GetByID(order.CustomerID ?? 0);
			var governorate = governorateRepository.GetByID(customer.GovernorateID ?? 0);

			orderDetailVM.bookCartItems = bookCartItems;
			orderDetailVM.CustomerName = customer.Name;
			orderDetailVM.TotalPrice = order.TotalAmount ?? 0;
			orderDetailVM.CustomerAddress = customer.Address;
			orderDetailVM.Governorate = governorate.Name;
			orderDetailVM.CustomerID = order.CustomerID;
			orderDetailVM.Nationalnumber = customer.Nationalnumber;

			return View("Details", orderDetailVM);
		}
		public IActionResult GetAll()
		{
			return View("GetAll", customerRepository.GetAll());
		}
		private string GetCustomerID()
		{
			if (User.Identity.IsAuthenticated)
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (!string.IsNullOrEmpty(userId))
				{
					return customerRepository.GetByUserID(userId).ID.ToString();
				}
			}

			var customerIDCookie = Request.Cookies["CustomerID"];
			if (customerIDCookie != null)
			{
				return JsonConvert.DeserializeObject<string>(customerIDCookie).ToString();
			}

			return string.Empty;
		}

	}
}