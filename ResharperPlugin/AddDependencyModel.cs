using System;
using System.Collections.Generic;
using JetBrains.ReSharper.Feature.Services.CSharp.Util;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace ResharperPlugin
{
    public class AddDependencyModel
    {
        private readonly ITypeValidator _typeValidator;

        public bool IsValid = true;
        public string NewParameterType;

        public AddDependencyModel()
        {
            _typeValidator = new CSharpTypeValidator();
        }

        public bool IsValidReturnType(string returnTypeText)
        {
            return _typeValidator.IsValidReturnType(returnTypeText);
        }
    }
}