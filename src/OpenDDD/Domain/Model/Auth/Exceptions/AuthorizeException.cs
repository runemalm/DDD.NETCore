using OpenDDD.Domain.Model.Error;

namespace OpenDDD.Domain.Model.Auth.Exceptions
{
    public class AuthorizeException : DomainException
    {
        public static AuthorizeException Forbidden()
            => new AuthorizeException(DomainError.Authorize_Forbidden());
        
        public static AuthorizeException InvalidCredentials(string reason)
            => new AuthorizeException(DomainError.Authorize_InvalidCredentials(reason));
        
        public static AuthorizeException MissingCredentials(string spec)
            => new AuthorizeException(DomainError.Authorize_MissingCredentials(spec));
        
        public static AuthorizeException EmailNotVerified()
            => new AuthorizeException(DomainError.Authorize_EmailNotVerified());
        
        public static AuthorizeException NotAuthenticated()
            => new AuthorizeException(DomainError.Authorize_NotAuthenticated());
        
        public static AuthorizeException InvalidRequest(string spec)
            => new AuthorizeException(DomainError.Authorize_InvalidRequest(spec));
        
        public static AuthorizeException TokenExpired()
            => new AuthorizeException(DomainError.Authorize_TokenExpired());

        public AuthorizeException(IDomainError error) : base(error)
        {
            
        }
    }
}
