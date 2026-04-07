using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
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
                    ValidateIssuer = true,
                    ValidIssuer = Consts.JWT_ISSUER,
                    ValidateAudience = true,
                    ValidAudience = Consts.JWT_AUDIENCE,
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



        /// <summary>
        /// Access Token 생성. 기본 만료: ACCESS_TOKEN_EXPIRY_MINUTES (15분).
        /// </summary>
        public static string GenerateJwtToken(int playerId, int expiryMinutes = Consts.ACCESS_TOKEN_EXPIRY_MINUTES)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Consts.SECRET_KEY);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, playerId.ToString()) }),
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Issuer = Consts.JWT_ISSUER,
                Audience = Consts.JWT_AUDIENCE,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Refresh Token 생성. 포맷: "{playerId}:{64바이트 랜덤 Base64}"
        /// playerId를 앞에 내장하여 Redis 키(refresh:{playerId})를 직접 계산할 수 있습니다.
        /// 이를 통해 역방향 인덱스 없이 재로그인 시 기존 토큰을 자동 덮어씁니다.
        /// </summary>
        public static string GenerateRefreshToken(int playerId)
        {
            string random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            return $"{playerId}:{random}";
        }

        /// <summary>
        /// Refresh Token에서 playerId를 파싱합니다. 포맷이 올바르지 않으면 null 반환.
        /// </summary>
        public static int? ParsePlayerIdFromRefreshToken(string refreshToken)
        {
            int colonIndex = refreshToken.IndexOf(':');
            if (colonIndex <= 0) return null;
            if (int.TryParse(refreshToken[..colonIndex], out int playerId))
                return playerId;
            return null;
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