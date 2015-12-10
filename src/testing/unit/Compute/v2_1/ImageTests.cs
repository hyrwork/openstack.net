﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using OpenStack.Compute.v2_1.Serialization;
using OpenStack.Serialization;
using OpenStack.Synchronous;
using OpenStack.Testing;
using Xunit;

namespace OpenStack.Compute.v2_1
{
    public class ImageTests
    {
        private readonly ComputeService _compute;

        public ImageTests()
        {
            _compute = new ComputeService(Stubs.AuthenticationProvider, "region");
        }
        
        [Fact]
        public void GetImage()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = "1";
                httpTest.RespondWithJson(new Image { Id = imageId });

                var result = _compute.GetImage(imageId);

                httpTest.ShouldHaveCalled($"*/images/{imageId}");
                Assert.NotNull(result);
                Assert.Equal(imageId, result.Id);
                Assert.IsType<ComputeApiBuilder>(((IServiceResource)result).Owner);
            }
        }

        [Fact]
        public void GetImageMetadata()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = "1";
                httpTest.RespondWithJson(new ImageMetadata { ["stuff"] = "things" });

                ImageMetadata result = _compute.GetImageMetadata(imageId);

                httpTest.ShouldHaveCalled($"*/images/{imageId}/metadata");
                Assert.NotNull(result);
                Assert.Equal(1, result.Count);
                Assert.True(result.ContainsKey("stuff"));
                Assert.IsType<ComputeApiBuilder>(((IServiceResource)result).Owner);
            }
        }

        [Fact]
        public void GetImageExtension()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new ImageReferenceCollection
                {
                    new ImageReference {Id = imageId}
                });
                httpTest.RespondWithJson(new Image { Id = imageId });

                var results = _compute.ListImages();
                var flavorRef = results.First();
                var result = flavorRef.GetImage();

                Assert.NotNull(result);
                Assert.Equal(imageId, result.Id);
            }
        }

        [Fact]
        public void WaitForImageActive()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Unknown });
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Saving });
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Active });

                var result = _compute.GetImage(imageId);
                result.WaitUntilActive();

                httpTest.ShouldHaveCalled($"*/images/{imageId}");
                Assert.NotNull(result);
                Assert.Equal(imageId, result.Id);
                Assert.Equal(ImageStatus.Active, result.Status);
                Assert.IsType<ComputeApiBuilder>(((IServiceResource)result).Owner);
            }
        }

        [Fact]
        public void ListImages()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new ImageReferenceCollection
                {
                    Items = { new ImageReference { Id = imageId } },
                    Links = { new PageLink("next", "http://api.com/next") }
                });

                var results = _compute.ListImages();

                httpTest.ShouldHaveCalled("*/images");
                Assert.Equal(1, results.Count());
                var result = results.First();
                Assert.Equal(imageId, result.Id);
                Assert.IsType<ComputeApiBuilder>(((IServiceResource)result).Owner);
            }
        }

        [Fact]
        public void ListImagesWithFilter()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWithJson(new ImageCollection());

                const string name = "foo";
                const int minRam = 2;
                const int minDisk = 1;
                Identifier serverId = Guid.NewGuid();
                var lastModified = DateTimeOffset.Now.AddDays(-1);
                var imageType = ImageType.Snapshot;
                
                _compute.ListImages(new ImageListOptions { Name = name, ServerId = serverId, LastModified = lastModified, MininumDiskSize = minDisk, MininumMemorySize = minRam, Type = imageType});

                httpTest.ShouldHaveCalled($"*name={name}");
                httpTest.ShouldHaveCalled($"*server={serverId}");
                httpTest.ShouldHaveCalled($"*minRam={minRam}");
                httpTest.ShouldHaveCalled($"*minDisk={minDisk}");
                httpTest.ShouldHaveCalled($"*type={imageType}");
                httpTest.ShouldHaveCalled("*changes-since=");
            }
        }

        [Fact]
        public void ListImagesWithPaging()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWithJson(new ImageCollection());

                Identifier startingAt = Guid.NewGuid();
                const int pageSize = 10;
                _compute.ListImages(new ImageListOptions { PageSize = pageSize, StartingAt = startingAt });

                httpTest.ShouldHaveCalled($"*marker={startingAt}*");
                httpTest.ShouldHaveCalled($"*limit={pageSize}*");
            }
        }

        [Theory]
        [InlineData(false, "POST")]
        [InlineData(true, "PUT")]
        public void UpdateImageMetadata(bool overwrite, string expectedHttpVerb)
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = "1";
                httpTest.RespondWithJson(new Image {Id = imageId});
                httpTest.RespondWithJson(new ImageMetadata {["stuff"] = "things" });

                var image = _compute.GetImage(imageId);
                image.Metadata["color"] = "blue";
                image.Metadata.Update(overwrite);

                httpTest.ShouldHaveCalled($"*/images/{imageId}/metadata");
                Assert.Equal(expectedHttpVerb, httpTest.CallLog.Last().Request.Method.Method);
                Assert.True(image.Metadata.ContainsKey("stuff"));
            }
        }

        [Fact]
        public void DeleteImage()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWith((int)HttpStatusCode.NoContent, "All gone!");

                _compute.DeleteImage(imageId);

                httpTest.ShouldHaveCalled($"*/images/{imageId}");
            }
        }

        [Fact]
        public void DeleteImageExtension()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new Image { Id = imageId });
                httpTest.RespondWith((int)HttpStatusCode.NoContent, "All gone!");
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Deleted });

                var image = _compute.GetImage(imageId);

                image.Delete();
                Assert.Equal(image.Status, ImageStatus.Unknown);

                image.WaitUntilDeleted();
                Assert.Equal(image.Status, ImageStatus.Deleted);
            }
        }

        [Fact]
        public void WhenDeleteImage_Returns404NotFound_ShouldConsiderRequestSuccessful()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWith((int)HttpStatusCode.NotFound, "Not here, boss...");

                _compute.DeleteImage(imageId);

                httpTest.ShouldHaveCalled($"*/images/{imageId}");
            }
        }

        [Fact]
        public void WaitForImageDeleted()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Active });
                httpTest.RespondWith((int)HttpStatusCode.NoContent, "All gone!");
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Deleted });

                var result = _compute.GetImage(imageId);
                result.Delete();
                result.WaitUntilDeleted();

                Assert.Equal(ImageStatus.Deleted, result.Status);
            }
        }

        [Fact]
        public void WaitForImageDeleted_Returns404NotFound_ShouldConsiderRequestSuccessful()
        {
            using (var httpTest = new HttpTest())
            {
                Identifier imageId = Guid.NewGuid();
                httpTest.RespondWithJson(new Image { Id = imageId, Status = ImageStatus.Active });
                httpTest.RespondWith((int)HttpStatusCode.NoContent, "All gone!");
                httpTest.RespondWith((int)HttpStatusCode.NotFound, "Nothing here, boss");

                var result = _compute.GetImage(imageId);
                result.Delete();
                result.WaitUntilDeleted();

                Assert.Equal(ImageStatus.Deleted, result.Status);
            }
        }
    }
}
