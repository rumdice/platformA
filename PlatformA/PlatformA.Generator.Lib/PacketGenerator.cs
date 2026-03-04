using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PlatformA.Generator.Lib
{
    // 이 클래스가 컴파일러 확장이 됨을 알리는 어트리뷰트
    [Generator]
    public class PacketGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new PacketReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // 1. PacketAttribute 주입 (생략 - 기존 코드 유지)
            // 1. 앞으로 우리가 구조체에 달아줄 [Packet] 속성 코드를 컴파일 타임에 주입합니다.
            var attributeSource = @"
using System;
namespace PlatformA.Game.Server.Packet
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class PacketAttribute : Attribute { }
}";
            // 이 파일은 물리적인 하드디스크에는 없지만, 컴파일할 때 메모리상에 생성되어 합쳐집니다.
            context.AddSource("PacketAttribute.g.cs", SourceText.From(attributeSource, Encoding.UTF8));

            if (!(context.SyntaxReceiver is PacketReceiver receiver)) return;

            foreach (var structDecl in receiver.CandidateStructs)
            {
                string structName = structDecl.Identifier.Text;
                var fields = structDecl.Members.OfType<FieldDeclarationSyntax>().ToList();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Buffers.Binary;");
                sb.AppendLine("namespace PlatformA.Game.Server.Packet {"); // 네임스페이스 통일
                sb.AppendLine($"    public partial struct {structName} {{");

                // --- Serialize 생성 ---
                sb.AppendLine("        public void Serialize(Span<byte> span) {");
                int offset = 0;
                foreach (var field in fields)
                {
                    string fieldName = field.Declaration.Variables.First().Identifier.Text;
                    string fieldType = field.Declaration.Type.ToString();
                    if (fieldType == "int")
                    {
                        sb.AppendLine($"            BinaryPrimitives.WriteInt32LittleEndian(span.Slice({offset}, 4), this.{fieldName});");
                        offset += 4;
                    }
                    else if (fieldType == "float")
                    {
                        sb.AppendLine($"            BinaryPrimitives.WriteSingleLittleEndian(span.Slice({offset}, 4), this.{fieldName});");
                        offset += 4;
                    }
                }
                sb.AppendLine("        }");

                // --- Deserialize 생성 ---
                sb.AppendLine("        public void Deserialize(ReadOnlySpan<byte> span) {");
                int decodeOffset = 0;
                foreach (var field in fields)
                {
                    string fieldName = field.Declaration.Variables.First().Identifier.Text;
                    string fieldType = field.Declaration.Type.ToString();
                    if (fieldType == "int")
                    {
                        sb.AppendLine($"            this.{fieldName} = BinaryPrimitives.ReadInt32LittleEndian(span.Slice({decodeOffset}, 4));");
                        decodeOffset += 4;
                    }
                    else if (fieldType == "float")
                    {
                        sb.AppendLine($"            this.{fieldName} = BinaryPrimitives.ReadSingleLittleEndian(span.Slice({decodeOffset}, 4));");
                        decodeOffset += 4;
                    }
                }
                sb.AppendLine("        }");

                sb.AppendLine("    }"); // struct 닫기
                sb.AppendLine("}");     // namespace 닫기

                // 🚨 중요: 모든 내용(Serialize + Deserialize)이 담긴 sb를 한 번만 AddSource 합니다.
                context.AddSource($"{structName}_Gen.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }
    }
}
