using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace PlatformA.Generator.Lib
{
    internal class PacketReceiver : ISyntaxReceiver
    {
        public List<StructDeclarationSyntax> CandidateStructs { get; } = new List<StructDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // 구조체이면서 속성(Attribute)이 달려있는지 확인
            if (syntaxNode is StructDeclarationSyntax structDecl && structDecl.AttributeLists.Count > 0)
            {
                // "Packet" 또는 "PacketAttribute"가 포함되어 있는지 확인
                var hasAttribute = structDecl.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().EndsWith("Packet") || a.Name.ToString().EndsWith("PacketAttribute"));

                if (hasAttribute)
                    CandidateStructs.Add(structDecl);
            }
        }
    }
}
