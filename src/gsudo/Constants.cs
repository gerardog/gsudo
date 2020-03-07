namespace gsudo
{
    class Constants
    {
        // All tokens must have small amount of chars, to avoid the token being split by the network chunking
        internal const string TOKEN_FOCUS = "\u0011"; 
        internal const string TOKEN_EXITCODE = "\u0012";
        internal const string TOKEN_ERROR = "\u0013";
        internal const string TOKEN_KEY_CTRLC = "\u0014";
        internal const string TOKEN_KEY_CTRLBREAK = "\u0015";
        internal const string TOKEN_SUCCESS = "\u0016";
        
        internal const int GSUDO_ERROR_EXITCODE = 999;
    }
}
