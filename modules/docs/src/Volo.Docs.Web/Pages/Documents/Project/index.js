(function ($) {

    $(function () {

        var initNavigationFilter = function (navigationContainerId) {
         
            var $navigation = $("#" + navigationContainerId);

            var getShownDocumentLinks = function () {
                return $navigation.find(".mCSB_container > li a:visible").not(".tree-toggle");
            };

            var gotoFilteredDocumentIfThereIsOnlyOne = function () {
                var $links = getShownDocumentLinks();
                if ($links.length === 1) {
                    var url = $links.first().attr("href");
                    if (url === "javascript:;") {
                        return;
                    }

                    window.location = url;
                }
            };

            var filterDocumentItems = function (filterText) {
                $navigation.find(".mCSB_container .opened").removeClass("opened");
                $navigation.find(".mCSB_container > li, .mCSB_container > li ul").hide();

                if (!filterText) {
                    $navigation.find(".mCSB_container > li").show();
                    $navigation.find(".mCSB_container .selected-tree > ul").show();
                    return;
                }

                var filteredItems = $navigation.find("li > a").filter(function () {
                    return $(this).text().toUpperCase().indexOf(filterText.toUpperCase()) > -1;
                });

                filteredItems.each(function () {

                    var $el = $(this);
                    $el.show();
                    var $parent = $el.parent();

                    var hasParent = true;
                    while (hasParent) {
                        if ($parent.attr("id") === navigationContainerId) {
                            break;
                        }

                        $parent.show();
                        $parent.find("> li > label").not(".last-link").addClass("opened");

                        $parent = $parent.parent();
                        hasParent = $parent.length > 0;
                    }
                });
            };

            $(".docs-page .docs-tree-list input[type='search']").keyup(function (e) {
                filterDocumentItems(e.target.value);

                if (e.key === "Enter") {
                    gotoFilteredDocumentIfThereIsOnlyOne();
                }
            });
        };

        var initAnchorTags = function (container) {
            anchors.options = {
                placement: 'left'
            };

            var anchorTags = ["h1", "h2", "h3", "h4", "h5", "h6"];
            anchorTags.forEach(function (tag) {
                anchors.add(container + " " + tag);
            });
        };

        var initSocialShareLinks = function () {
            var pageHeader = $(".docs-body").find("h1, h2").first().text();

            var projectName = $('#ProjectName')[0].innerText;

            $('#TwitterShareLink').attr('href',
                'https://twitter.com/intent/tweet?text=' + encodeURI(pageHeader + " | " + projectName + " | " + window.location.href)
            );

            $('#WeiboShareLink').attr('href',
                'https://service.weibo.com/share/share.php?'
                + 'url=' + encodeURI(window.location.href) + '&'
                + "title=" + encodeURI(pageHeader + ' - ABP中文网 https://cn.abp.io/')
            );

            $('#WechatShareLink').tooltip({
                html:true,
                title:"<div id='WechatQRCode'>微信扫一扫分享</div>"
            }).on('shown.bs.tooltip', function () {
                new QRCode(document.getElementById("WechatQRCode"), {
                    text: encodeURI(window.location.href),
                    width: 128,
                    height: 128
                });
            });
        };

        var initAD = function(){
            var tencent = '<div class="mt-2 mr-1"><a href="https://cloud.tencent.com/redirect.php?redirect=1025&cps_key=0830ceeb17f8c52968a1279ba57c36a2&from=console" target="_blank"><img src="/assets/tencentcloud.jpg" class="w-100"></a></div>';
            var aliyun = '<div class="mt-2 mr-1"><a href="https://promotion.aliyun.com/ntms/act/qwbk.html?userCode=n7xgclf4" target="_blank"><img src="/assets/aliyun.png" class="w-100"></a></div>';

            $('#scroll-index').append($(tencent)).append($(aliyun));
        };

        initNavigationFilter("sidebar-scroll");

        initAnchorTags(".docs-page .docs-body");

        initSocialShareLinks();

        //initAD();

    });

})(jQuery);

