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
        internal const string TOKEN_EOF = "\u0017";
        internal const int GSUDO_ERROR_EXITCODE = 999;
        internal const string TI_SID = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    }
}
