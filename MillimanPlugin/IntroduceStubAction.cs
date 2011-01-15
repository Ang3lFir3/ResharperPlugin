using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CSharp.Stages;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Intentions;
using JetBrains.ReSharper.Intentions.CSharp.Util;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeStyle;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.ExpectedTypes;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace MillimanPlugin
{
    [QuickFix(0x7FFF)]
    public class CreateLocalVarFromUsageFix : BulbItemImpl, IQuickFix
    {
        // Fields
        private readonly IReference myReference;
        private readonly IReferenceExpression myReferenceExpression;

        // Methods
        public CreateLocalVarFromUsageFix(NotResolvedError error)
        {
            myReference = error.Reference;
            myReferenceExpression = (myReference == null) ? null : (myReference.GetElement() as IReferenceExpression);
        }

        private bool InsideConstantExpression
        {
            get { return (ReferenceExpression.GetContainingElement<IGotoCaseStatement>(false) != null); }
        }

        private IReference Reference
        {
            get { return myReference; }
        }

        private IReferenceExpression ReferenceExpression
        {
            get { return myReferenceExpression; }
        }

        public override string Text
        {
            get { return GetText("RhinoMocks stub", false); }
        }

        #region IQuickFix Members

        public bool IsAvailable(IUserDataHolder cache)
        {
            if (InvocationExpressionNavigator.GetByInvokedExpression(myReferenceExpression) != null)
                return false;
            
            if (myReferenceExpression == null)
                return false;
            
            var type = Reference.CheckResolveResult();
            if ((type != ResolveErrorType.NOT_RESOLVED) && (type != ResolveErrorType.WRONG_NAME_CASE))
                return false;

            if (InsideConstantExpression)
                return false;
            
            return ((ReferenceExpression.QualifierExpression == null) && (GetAnchor() != null));
        }

        public new IBulbItem[] Items
        {
            get { return new[] {this}; }
        }

        #endregion

        private IList<ICSharpExpression> CollectUsages(IElement scope)
        {
            return FilterUsages(
                ReferencesCollectingUtil.CollectElementsWithUnresolvedReference(
                    scope,
                    ReferenceExpression.Reference.GetName(),
                    (IReferenceExpression expression) => expression.Reference));
        }

        protected override Action<ITextControl> ExecuteTransaction(ISolution solution, IProgressIndicator progress)
        {
            var instance = CSharpElementFactory.GetInstance(ReferenceExpression.GetPsiModule());
            var anchor = GetAnchor();
            var typeConstraint = VariableTypeConstraint(anchor);
            var elements = CollectUsages(anchor);
            var statementToBeVisibleFromAll = ExpressionUtil.GetStatementToBeVisibleFromAll(elements);
            var declarationStatement = GetDeclarationStatement(statementToBeVisibleFromAll, instance, elements, typeConstraint);
            declarationStatement.LanguageService.CodeFormatter.Format(declarationStatement.ToTreeNode(),
                                                                      CodeFormatProfile.GENERATOR);
            return control => CSharpTemplateUtil.ExecuteTemplate(
                solution,
                control,
                typeConstraint,
                declarationStatement.VariableDeclarations[0].ToTreeNode().TypeUsage, 
                true,
                new TextRange(declarationStatement.VariableDeclarations[0].GetNameDocumentRange().TextRange.EndOffset));
        }

        private static IList<ICSharpExpression> FilterUsages(IEnumerable<IReferenceExpression> expressions)
        {
            var list = new List<ICSharpExpression>();
            foreach (var expression in expressions)
                if (InvocationExpressionNavigator.GetByInvokedExpression(expression) == null)
                    list.Add(expression);
            return list;
        }

        private IBlock GetAnchor()
        {
            IList<ICSharpExpression> list = null;
            IBlock block = null;
            for (var block2 = ReferenceExpression.GetContainingElement<IBlock>(false);
                 block2 != null;
                 block2 = block2.GetContainingElement<IBlock>(false))
            {
                if (SwitchStatementNavigator.GetByBlock(block2) == null)
                {
                    IList<ICSharpExpression> list2 = CollectUsages(block2);
                    if ((list == null) || (list.Count < list2.Count))
                    {
                        list = list2;
                        block = block2;
                    }
                }
            }
            return block;
        }

        private IDeclarationStatement GetDeclarationStatement(IStatement anchor, CSharpElementFactory factory,
                                                              IList<ICSharpExpression> usages, IExpectedTypeConstraint typeConstraint)
        {
            try
            {
                var statement = (IDeclarationStatement)factory.CreateStatement(
                    "var $0 = MockRepository.GenerateStub<$1>();",
                    Reference.GetName(),
                    typeConstraint.GetDefaultTypes().First().GetPresentableName(anchor.Language));
                if (statement == null)
                    throw new InvalidOperationException("Not expected to be possible!");
                return StatementUtil.InsertStatement(statement, ref anchor, true);
            }
            catch(Exception ex)
            {
                File.AppendAllText("c:\\temp\\MillimanPluginErrors.txt", "Exception on " + DateTime.Now + "\n" + ex + "\n\n");
                throw;
            }
        }

        private string GetText(string entityString, bool useContext)
        {
            string name = Reference.GetName();
            if (useContext)
            {
                IReference reference = myReference;
                if ((reference != null) && IsQualified(reference.GetAccessContext()))
                {
                    ITypeElement qualifierTypeElement = reference.GetAccessContext().GetQualifierTypeElement();
                    if (qualifierTypeElement != null)
                    {
                        name =
                            DeclaredElementPresenter.Format(reference.GetElement().Language,
                                                            DeclaredElementPresenter.NAME_PRESENTER,
                                                            qualifierTypeElement) + "." + name;
                    }
                }
            }
            return string.Format("Create {0} '{1}'", entityString, name);
        }

        private static bool IsQualified(IAccessContext context)
        {
            return context.GetQualifierKind() != QualifierKind.NONE;
        }

        private IExpectedTypeConstraint VariableTypeConstraint(IElement scope)
        {
            return ExpectedTypesUtil.GuessTypes(CollectUsages(scope).ConvertList<ICSharpExpression, IExpression>());
        }
    }
}