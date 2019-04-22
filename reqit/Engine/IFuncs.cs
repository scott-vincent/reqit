using reqit.Models;

namespace reqit.Engine
{
    public interface IFuncs
    {
        string FuncDate(string called, string[] args);
        string FuncGen(string called, string[] args);
        string FuncIf(string called, string[] args, Cache cache, string parent, IResolver resolver);
        string FuncMath(string called, string[] args, Cache cache, string parent, IResolver resolver);
        string FuncNum(string called, string[] args, Cache cache, string parent, IResolver resolver);
        string FuncPick(string called, string[] args);
        string FuncRand(string called, string[] args);
        string FuncRef(string called, string[] args, Cache cache, string parent, IResolver resolver,
                out Sample.Genders gender);
        string FuncSample(string called, string[] args, Cache cache, string parent, IResolver resolver,
                out Sample.Genders gender);
        string FuncStr(string called, string[] args);
        string FuncTime(string called, string[] args);
    }
}