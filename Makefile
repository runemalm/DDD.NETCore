##########################################################################
# This is the project's makefile.
#
# Simply run 'make' on the command line to list all available tasks.
##########################################################################

##########################################################################
# CONFIGURATION
##########################################################################

# Load env file
include env.make
export $(shell sed 's/=.*//' env.make)

##########################################################################
# VARIABLES
##########################################################################
HOME := $(shell echo ~)
PWD := $(shell pwd)
NETWORK := openddd-net
BUILD_VERSION := 3.0.0-beta.1

NUGET_NAME := OpenDDD.NET
ROOT_NAMESPACE := OpenDDD

SRC_DIR := $(PWD)/src
DOCS_DIR := $(PWD)/docs
SAMPLES_DIR := $(PWD)/samples
NAMESPACE_DIR := $(SRC_DIR)/$(ROOT_NAMESPACE)
BUILD_DIR := $(NAMESPACE_DIR)/$(ROOT_NAMESPACE)/bin
FEED_DIR := $(HOME)/Projects/LocalFeed
USER_NUGET_CONFIG_DIR=$(HOME)/.config/NuGet/NuGet.Config
SPHINXDOC_IMG := openddd.net/sphinxdoc

DOCSAUTOBUILD_HOST_NAME := docsautobuild-openddd.net
DOCSAUTOBUILD_CONTAINER_NAME := docsautobuild-openddd.net
DOCSAUTOBUILD_PORT := 10001

TEMPLATES_DIR := $(PWD)/templates
TEMPLATES_CSPROJ := $(TEMPLATES_DIR)/templatepack.csproj
TEMPLATES_OUT := $(TEMPLATES_DIR)/bin/templates
TEMPLATES_NAME := OpenDDD.NET-Templates
TEMPLATES_VERSION := 3.0.0-alpha.1
TEMPLATES_NUPKG := $(TEMPLATES_OUT)/$(TEMPLATES_NAME).$(TEMPLATES_VERSION).nupkg

BLUE      := $(shell tput -Txterm setaf 4)
GREEN     := $(shell tput -Txterm setaf 2)
TURQUOISE := $(shell tput -Txterm setaf 6)
WHITE     := $(shell tput -Txterm setaf 7)
YELLOW    := $(shell tput -Txterm setaf 3)
GREY      := $(shell tput -Txterm setaf 1)
RESET     := $(shell tput -Txterm sgr0)
SMUL      := $(shell tput smul)
RMUL      := $(shell tput rmul)

# Add the following 'help' target to your Makefile
# And add help text after each target name starting with '\#\#'
# A category can be added with @category
HELP_FUN = \
	%help; \
	use Data::Dumper; \
	while(<>) { \
		if (/^([a-zA-Z\-_0-9]+)\s*:.*\#\#(?:@([a-zA-Z\-0-9\.\s]+))?\t(.*)$$/) { \
			$$c = $$2; $$t = $$1; $$d = $$3; \
			push @{$$help{$$c}}, [$$t, $$d, $$ARGV] unless grep { grep { grep /^$$t$$/, $$_->[0] } @{$$help{$$_}} } keys %help; \
		} \
	}; \
	for (sort keys %help) { \
		printf("${WHITE}%24s:${RESET}\n\n", $$_); \
		for (@{$$help{$$_}}) { \
			printf("%s%25s${RESET}%s  %s${RESET}\n", \
				( $$_->[2] eq "Makefile" || $$_->[0] eq "help" ? "${YELLOW}" : "${GREY}"), \
				$$_->[0], \
				( $$_->[2] eq "Makefile" || $$_->[0] eq "help" ? "${GREEN}" : "${GREY}"), \
				$$_->[1] \
			); \
		} \
		print "\n"; \
	}

# make
.DEFAULT_GOAL := help

# Variables
PWD = $(shell pwd)

# Variable wrapper
define defw
	custom_vars += $(1)
	$(1) ?= $(2)
	export $(1)
	shell_env += $(1)="$$($(1))"
endef

.PHONY: help
help:: ##@Other Show this help.
	@echo ""
	@printf "%30s " "${YELLOW}TARGETS"
	@echo "${RESET}"
	@echo ""
	@perl -e '$(HELP_FUN)' $(MAKEFILE_LIST)

##########################################################################
# TEST
##########################################################################
.PHONY: test
test: ##@Test	 run all unit tests
	ENV_FILE=env.test dotnet test $(TESTS_DIR)

##########################################################################
# BUILD
##########################################################################
.PHONY: clean
clean: ##@Build	 clean the solution
	find . $(SRC_DIR) -iname "bin" | xargs rm -rf
	find . $(SRC_DIR) -iname "obj" | xargs rm -rf

.PHONY: build
build: ##@Build	 build the solution
	cd $(SRC_DIR) && \
	dotnet build

.PHONY: deep-rebuild
deep-rebuild: ##@Build	 clean, clear nuget caches, restore and build the project
	make clean
	make clear-nuget-caches
	make restore
	make build

.PHONY: pack
pack: ##@Build	 Create the nuget in local feed
	make build
	cd $(SRC_DIR) && \
	dotnet pack -c Release -o $(FEED_DIR) -p:PackageVersion=$(BUILD_VERSION)

.PHONY: push
push: ##@Build	 Push the nuget to the global feed
	cd $(FEED_DIR) && \
	dotnet nuget push $(NUGET_NAME).$(BUILD_VERSION).nupkg --api-key $(NUGET_API_KEY) --source https://api.nuget.org/v3/index.json

##########################################################################
# DOCS
##########################################################################

.PHONY: sphinx-buildimage
sphinx-buildimage: ##@Docs	 Build the custom sphinxdoc image
	docker build -t $(SPHINXDOC_IMG) $(DOCS_DIR)

.PHONY: sphinx-html
sphinx-html: ##@Docs	 Build the sphinx html
	docker run -it --rm -v $(DOCS_DIR):/docs $(SPHINXDOC_IMG) make html

.PHONY: sphinx-clean
sphinx-clean: ##@Docs	 Clean the sphinx docs
	rm -rf $(DOCS_DIR)/_build

.PHONY: sphinx-rebuild
sphinx-rebuild: ##@Docs	 Re-build the sphinx docs
	make sphinx-clean && \
	make sphinx-html

.PHONY: sphinx-autobuild
sphinx-autobuild: ##@Docs	 Activate autobuild of docs
	docker run \
		-it \
		--rm \
		--name $(DOCSAUTOBUILD_CONTAINER_NAME) \
		--hostname $(DOCSAUTOBUILD_HOST_NAME) \
		-p "$(DOCSAUTOBUILD_PORT):8000" \
		-v $(DOCS_DIR):/docs \
		$(SPHINXDOC_IMG) \
		sphinx-autobuild /docs /docs/_build/html

.PHONY: sphinx-opendocs
sphinx-opendocs: ##@Docs	 Open the docs in browser
	open $(DOCS_DIR)/_build/html/index.html

##########################################################################
# .NET
##########################################################################
.PHONY: restore
restore: ##@Build	 restore the solution
	cd src && dotnet restore

.PHONY: clear-nuget-caches
clear-nuget-caches: ##@Build	 clean all nuget caches
	nuget locals all -clear

##########################################################################
# TEMPLATES
##########################################################################

.PHONY: templates-install
templates-install: ##@Template	 Install the OpenDDD.NET project template locally
	dotnet new install $(TEMPLATES_NUPKG)

.PHONY: templates-uninstall
templates-uninstall: ##@Template	 Uninstall the OpenDDD.NET project template
	dotnet new uninstall $(TEMPLATES_NAME)

.PHONY: templates-pack
templates-pack: ##@Template	 Pack the OpenDDD.NET project template into a NuGet package
	dotnet pack $(TEMPLATES_CSPROJ) -o $(TEMPLATES_OUT)

.PHONY: templates-publish
templates-publish: template-pack ##@Template	 Publish the template to NuGet
	dotnet nuget push $(TEMPLATES_NUPKG) --api-key $(NUGET_API_KEY) --source https://api.nuget.org/v3/index.json

.PHONY: templates-rebuild
templates-rebuild: templates-uninstall templates-pack templates-install ##@Template	 Rebuild and reinstall the template
