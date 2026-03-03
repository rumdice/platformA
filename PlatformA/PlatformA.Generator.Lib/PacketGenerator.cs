using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace PlatformA.Generator.Lib
{
    // 이 클래스가 컴파일러 확장이 됨을 알리는 어트리뷰트
    [Generator]
    public class PacketGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // 초기화 단계
            // TODO..
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // 1. 앞으로 우리가 구조체에 달아줄 [Packet] 속성 코드를 컴파일 타임에 주입합니다.
            var attributeSource = @"
using System;
namespace PlatformA.Game.Server
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class PacketAttribute : Attribute { }
}";
            // 이 파일은 물리적인 하드디스크에는 없지만, 컴파일할 때 메모리상에 생성되어 합쳐집니다.
            context.AddSource("PacketAttribute.g.cs", SourceText.From(attributeSource, Encoding.UTF8));




            // 2. 소스 생성기가 잘 동작하는지 확인하기 위한 더미 클래스 주입
            var dummySource = @"
using System;
namespace PlatformA.Game.Server
{
    public static class GeneratorTest
    {
        public static void Hello()
        {
            Console.WriteLine(""[Source Generator] 컴파일 타임 마법이 성공적으로 작동했습니다!"");
        }
    }
}";
            context.AddSource("GeneratorTest.g.cs", SourceText.From(dummySource, Encoding.UTF8));
        }
    }
}
