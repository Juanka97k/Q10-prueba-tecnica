using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.DTOs;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

/// <summary>
/// Controlador HTTP para la gestión de pedidos e inventarios de stock en tiempo real.
/// </summary>
[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<CreateOrderRequest> _validator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        IValidator<CreateOrderRequest> validator,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Registra un nuevo pedido en estado Pendiente y dispara el evento asíncrono a RabbitMQ.
    /// </summary>
    /// <param name="request">Datos del pedido a crear (Cliente, SKU y Cantidad).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Objeto OrderResponse con estado HTTP 201 Created.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Petición de creación de orden rechazada por validaciones.");
            var dictionary = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return ValidationProblem(new ValidationProblemDetails(dictionary));
        }

        try
        {
            var response = await _orderService.CreateOrderAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetOrderById), new { id = response.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al procesar la creación del pedido.");
            return Problem(
                detail: ex.Message,
                title: "Error al procesar la orden",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Obtiene el historial completo de pedidos ordenados descendentemente por fecha.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(cancellationToken);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el listado de pedidos.");
            return Problem(
                detail: ex.Message,
                title: "Error al consultar pedidos",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Obtiene el catálogo dinámico de existencias de stock desde PostgreSQL.
    /// </summary>
    [HttpGet("stocks")]
    [ProducesResponseType(typeof(IEnumerable<StockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken)
    {
        try
        {
            var stocks = await _orderService.GetStocksAsync(cancellationToken);
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el listado de inventario de stock.");
            return Problem(
                detail: ex.Message,
                title: "Error al consultar inventario de stock",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Obtiene un pedido específico mediante su identificador único (GUID).
    /// </summary>
    /// <param name="id">GUID del pedido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
            if (order == null)
            {
                return Problem(
                    detail: $"No se encontró el pedido con ID '{id}'.",
                    title: "Pedido no encontrado",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el pedido con ID {OrderId}.", id);
            return Problem(
                detail: ex.Message,
                title: "Error al consultar el pedido",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
