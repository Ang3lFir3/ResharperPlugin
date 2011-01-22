using System;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Intentions;
using JetBrains.ReSharper.Intentions.CSharp.DataProviders;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace ResharperPlugin
{
    [ContextAction(Description = "Add Arg<T> to this argument", Name = "AddArgToArgument", Priority = 0x7FFF)]
    public class AddRhinoArgToArgumentContextAction: BulbItemImpl, IContextAction
    {
        private readonly ICSharpContextActionDataProvider _provider;

        public AddRhinoArgToArgumentContextAction(ICSharpContextActionDataProvider provider)
        {
            _provider = provider;
        }

        protected override Action<ITextControl> ExecuteTransaction(ISolution solution, IProgressIndicator progress)
        {
            var item = _provider.GetSelectedElement<ICSharpArgument>(false, false);
            if (item == null)
                return null;

            var owner = item.GetContainingElement<ICSharpArgumentsOwner>(false);
            if (owner == null)
                return null;

            var type = item.GetExpressionType();
            if(type == null)
                return null;

            var typeName = type.GetLongPresentableName(owner.Language);
            if(typeName == null)
                return null;

            var expressionText = item.GetText();
            if (expressionText == null)
                return null;

            var factory = JetBrains.ReSharper.Psi.CSharp.CSharpElementFactory.GetInstance(owner.PsiModule);
            var newExpression = factory.CreateExpressionAsIs(string.Format("Arg<{0}>.Is.Equal({1})", typeName, expressionText));
            if (newExpression == null)
                return null;

            var newArgument = factory.CreateArgument(JetBrains.ReSharper.Psi.ParameterKind.VALUE, newExpression);
            if (newArgument == null)
                return null;

            item.ReplaceBy(newArgument);

            return null;
        }

        public override string Text
        {
            get { return "Convert to Arg<T>.Is.Equal() argument"; }
        }

        public bool IsAvailable(IUserDataHolder cache)
        {
            var item = _provider.GetSelectedElement<IArgument>(false, false);
            if (item == null)
                return false;

            var owner = item.GetContainingElement<IArgumentsOwner>(false);
            if (owner == null)
                return false;

            var ownerOfOwner = owner.GetContainingElement<IArgumentsOwner>(false);
            if (ownerOfOwner == null)
                return false;

            var invocation = ownerOfOwner as IInvocationExpression;
            if (invocation == null || invocation.Reference == null)
                return false;

            switch(invocation.Reference.GetName())
            {
                case "Stub":
                case "AssertWasCalled":
                case "AssertWasNotCalled":
                    break;
                default:
                    return false;
            }

            if(item.Expression is IInvocationExpression)
            {
                var argInvocation = (IInvocationExpression) item.Expression;
                var expression = argInvocation.InvokedExpression as IReferenceExpression;
                if(expression != null)
                {
                    var qualifier = expression.QualifierExpression as IReferenceExpression;
                    if (qualifier != null && qualifier.GetText().StartsWith("Arg<")) 
                        return false;
                    return true;
                }
            }

            if(item.Expression is IReferenceExpression)
            {
                var refExpression = (IReferenceExpression) item.Expression;
                var qualifier = refExpression.QualifierExpression as IReferenceExpression;
                if (qualifier != null && qualifier.GetText().StartsWith("Arg<")) 
                    return false;
            }

            return true;
        }
    }
}