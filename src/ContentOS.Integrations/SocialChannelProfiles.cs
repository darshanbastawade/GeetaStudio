using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public sealed class SocialChannelProfiles : ISocialCapabilityCatalog
{
    // Conservative local export profiles, not an assertion of every account's current API entitlement.
    public static SocialCapability[] Catalog =>
    [
        new("youtube.export.v1",1,"YouTube",["Short","Video"],100,5000,1,["https://www.googleapis.com/auth/youtube.upload"],"https://developers.google.com/youtube/v3/docs/videos/insert","OfficialDocsChecked2026-10-13;LiveAccountUnverified"),
        new("instagram.export.v1",1,"Instagram",["Short","Carousel","Image"],100,2200,10,["instagram_business_basic","instagram_business_content_publish"],"https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/content-publishing","OfficialPageUnavailable;LiveRulesRequireVerification"),
        new("facebook.export.v1",1,"Facebook",["Article","Short","Carousel","Image","Link"],100,5000,10,["pages_manage_posts","pages_read_engagement"],"https://developers.facebook.com/docs/pages-api/posts/","OfficialPage429;LiveRulesRequireVerification"),
        new("x.export.v1",1,"X",["Link","Image","Short"],100,280,4,["tweet.read","tweet.write","users.read"],"https://docs.x.com/x-api/posts/create-post","OfficialDocsChecked2026-10-13;LiveAccountUnverified")
    ];
    public static SocialCapability Capability(string key) => Catalog.SingleOrDefault(x => x.Key == key) ?? throw new RuntimeValidationException("Deployed versioned channel capability required.");

    public SocialCapability[] All => Catalog;
    public SocialCapability Resolve(string key) => Capability(key);
}
