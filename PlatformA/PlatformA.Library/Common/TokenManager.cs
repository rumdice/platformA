using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Common
{
    public class TokenManager
    {
        // ⚠️ 주의: 이 키는 Auth.API 서버에서 토큰을 만들 때 사용한 키와 100% 동일해야 합니다!
        // (실제 상용에서는 appsettings.json이나 환경변수에서 불러옵니다)
        
        /// <summary>
        /// 토큰을 검증하고 위조되지 않았다면 유저 ID(int)를 반환합니다. 실패 시 0 반환.
        /// </summary>
        public static int ValidateTokenAndGetUserId(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Consts.SECRET_KEY);

            try
            {
                // 🚀 1. ValidateToken의 반환값인 ClaimsPrincipal을 받습니다.
                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    //ClockSkew = TimeSpan.Zero
                    ClockSkew = TimeSpan.FromMinutes(5)
                }, out SecurityToken validatedToken);

                // 🚀 2. principal.FindFirst 를 사용하면 압축된 이름("nameid")을 원래 이름으로 알아서 매핑해 줍니다!
                // 혹시 몰라서 "nameid"나 "sub"로 들어왔을 경우도 대비해 안전하게 꺼냅니다.
                var claim = principal.FindFirst(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirst("nameid")
                         ?? principal.FindFirst("sub");

                if (claim != null)
                {
                    return int.Parse(claim.Value);
                }

                Console.WriteLine("[Token Error] 토큰 안에 유저 ID(Claim)가 존재하지 않습니다.");
                return 0;
            }
            catch (Exception ex)
            {
                // 토큰이 만료되었거나, 위조되었거나, 키가 다르면 여기로 빠집니다.
                Console.WriteLine($"[Token Error] 인증 실패: {ex.Message}");
                return 0;
            }
        }



        // JWT 토큰 생성기 (더미 클라이언트에서 쓰던 것과 완전히 동일)
        public static string GenerateJwtToken(int playerId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Consts.SECRET_KEY);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, playerId.ToString()) }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }


        // JWT 토큰 생성기 (더미 클라이언트에서 쓰던 것과 완전히 동일) - 테스트용 유저 이름 기반
        //public static string GenerateJwtTokenByUserName(string _username)
        //{
        //    var tokenHandler = new JwtSecurityTokenHandler();
        //    var key = Encoding.ASCII.GetBytes(Consts.SECRET_KEY);
        //    var tokenDescriptor = new SecurityTokenDescriptor
        //    {
        //        Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _username) }),
        //        Expires = DateTime.UtcNow.AddHours(1),
        //        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        //    };
        //    var token = tokenHandler.CreateToken(tokenDescriptor);
        //    return tokenHandler.WriteToken(token);
        //}
    }
}