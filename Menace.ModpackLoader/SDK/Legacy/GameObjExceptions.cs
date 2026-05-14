using System;

namespace Menace.SDK;

public class GameObjException : Exception
{
    public GameObjException(string message) : base(message) { }
    public GameObjException(string message, Exception inner) : base(message, inner) { }
}