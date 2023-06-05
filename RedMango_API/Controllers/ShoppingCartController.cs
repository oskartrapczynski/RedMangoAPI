using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedMango_API.Data;
using RedMango_API.Models;
using System.Diagnostics.Eventing.Reader;
using System.Net;

namespace RedMango_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShoppingCartController : ControllerBase
    {
        protected ApiResponse _response;
        private readonly ApplicationDBContext _dbContext;
        public ShoppingCartController(ApplicationDBContext dbContext)
        {
            _response = new();
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse>> GetShoppingCart(string userId)
        {
            try
            {
                ShoppingCart shoppingCart;
                if (string.IsNullOrEmpty(userId))
                {
                    shoppingCart = new();
                }
                else { 
                    shoppingCart = _dbContext.ShoppingCarts
                    .Include(x => x.CartItems).ThenInclude(x => x.MenuItem)
                    .FirstOrDefault(x => x.UserId == userId );
                }

                if (shoppingCart != null && shoppingCart.CartItems.Count > 0) 
                {
                    shoppingCart.CartTotal = shoppingCart.CartItems.Sum(x => x.Quantity * x.MenuItem.Price);
                }
                

                _response.Result = shoppingCart;
                _response.StatusCode = HttpStatusCode.OK;
                return Ok(_response);
            }
            catch(Exception ex)
            { 
                _response.IsSuccess = false;
                _response.ErrorMessages = 
                    new List<string>() { ex.ToString() };
                _response.StatusCode = HttpStatusCode.BadRequest;
            }
            return _response;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse>> AddOrUpdateItemInCart(string userId, int menuItemId, int updateQuantityBy)
        {
            ShoppingCart shoppingCart = _dbContext.ShoppingCarts.Include(u => u.CartItems).FirstOrDefault(u => u.UserId == userId);
            MenuItem menuItem = _dbContext.MenuItems.FirstOrDefault(u => u.Id == menuItemId);
            if (menuItem == null) {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                return BadRequest();
            }

            if (shoppingCart == null && updateQuantityBy > 0) 
            { 
                ShoppingCart newCart = new() { UserId = userId };
                _dbContext.ShoppingCarts.Add(newCart);
                _dbContext.SaveChanges();

                CartItem newCartItem = new()
                {
                    MenuItemId = menuItemId,
                    Quantity = updateQuantityBy,
                    ShoppingCartId = newCart.Id,
                    MenuItem = null
                };
                _dbContext.CartItems.Add(newCartItem);
                _dbContext.SaveChanges();
            }
            else
            {
                //shopping cart exists
                CartItem cartItemInCart = shoppingCart.CartItems.FirstOrDefault(u => u.MenuItemId == menuItemId);

                if (cartItemInCart == null) 
                {
                    // item does not exist in current cart
                    CartItem newCartItem = new()
                    {
                        MenuItemId= menuItemId,
                        Quantity = updateQuantityBy,
                        ShoppingCartId = shoppingCart.Id,
                        MenuItem = null
                    };
                    _dbContext.CartItems.Add(newCartItem);
                    _dbContext.SaveChanges();
                } else
                { 
                    // item arleady exist in the cart and we have to update quantity
                    int newQuantity = cartItemInCart.Quantity + updateQuantityBy;
                    if(updateQuantityBy == 0 || newQuantity <=0)
                    {
                        //remove cart item from cart and if it is the only item then remove cart 
                        _dbContext.CartItems.Remove(cartItemInCart);
                        if(shoppingCart.CartItems.Count == 1)
                        {
                            _dbContext.ShoppingCarts.Remove(shoppingCart);
                        }
                        _dbContext.SaveChanges();
                    }
                    else
                    {
                        cartItemInCart.Quantity = newQuantity;
                        _dbContext.SaveChanges();
                    }
                }
                

            }

            return _response;
        }
    }
}
