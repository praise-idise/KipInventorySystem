using System.Net;

namespace KipInventorySystem.Shared.Models;

public class ServiceResponse : ServiceResponseBase
{
    public static ServiceResponse Success(string? Message = null)
       => Success<ServiceResponse>(Message);

    public static ServiceResponse Created(string? Message = null)
        => Created<ServiceResponse>(Message);

    public static ServiceResponse Error(string Message)
        => Error<ServiceResponse>(Message);

    public static ServiceResponse BadRequest(string Message)
        => BadRequest<ServiceResponse>(Message);

    public static ServiceResponse Forbidden(string Message)
        => Forbidden<ServiceResponse>(Message);

    public static ServiceResponse Unauthorized(string Message)
        => Unauthorized<ServiceResponse>(Message);

    public static ServiceResponse NotFound(string Message)
        => NotFound<ServiceResponse>(Message);

    public static ServiceResponse Conflict(string Message)
        => Conflict<ServiceResponse>(Message);

    public static ServiceResponse Unavailable(string Message)
        => Unavailable<ServiceResponse>(Message);
}

public class ServiceResponse<T> : ServiceResponseBase
{
    public T? Data { get; set; }

    public static ServiceResponse<T> Success(T Data, string? Message = null)
    {
        var res = Success<ServiceResponse<T>>(Message);
        res.Data = Data;
        return res;
    }

    public static ServiceResponse<T> Created(T Data, string? Message = null)
    {
        var res = Created<ServiceResponse<T>>(Message);
        res.Data = Data;
        return res;
    }

    public static ServiceResponse<T> NotFound(string Message)
        => NotFound<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> Conflict(string Message)
        => Conflict<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> BadRequest(string Message)
        => BadRequest<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> Unauthorized(string Message)
        => Unauthorized<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> Forbidden(string Message)
        => Forbidden<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> Unavailable(string Message)
        => Unavailable<ServiceResponse<T>>(Message);

    public static ServiceResponse<T> Error(string Message)
        => Error<ServiceResponse<T>>(Message);
}


public class ServiceResponseBase
{
    public string? Message { get; set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public bool Succeeded =>
        StatusCode == HttpStatusCode.OK || StatusCode == HttpStatusCode.Created;

    protected static T CreateResponse<T>(HttpStatusCode Code, string? Message = null)
        where T : ServiceResponseBase, new()
        => new() { StatusCode = Code, Message = Message };

    public static T Success<T>(string? Message = null) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.OK, Message);

    public static T Created<T>(string? Message = null) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.Created, Message);

    public static T BadRequest<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.BadRequest, Message);

    public static T Conflict<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.Conflict, Message);

    public static T NotFound<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.NotFound, Message);

    public static T Unauthorized<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.Unauthorized, Message);

    public static T Forbidden<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.Forbidden, Message);

    public static T Unavailable<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.ServiceUnavailable, Message);

    public static T Error<T>(string Message) where T : ServiceResponseBase, new()
        => CreateResponse<T>(HttpStatusCode.InternalServerError, Message);
}
