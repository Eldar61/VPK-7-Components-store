--
-- PostgreSQL database dump
--

\restrict G1LfnD9M1r9VRofb1BIbalf7V3Pakogg6HXD1sRmZXH5ec1h135A7O3viGZrqn1

-- Dumped from database version 17.11
-- Dumped by pg_dump version 17.11

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Carts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Carts" (
    "Id" uuid NOT NULL,
    "ExternalConfigurationId" uuid,
    "Owner" text NOT NULL,
    "Status" text NOT NULL,
    "ItemsJson" text NOT NULL,
    "QuoteJson" text,
    "ReservedProductsJson" text,
    "ReservationId" uuid,
    "ExpiresAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ConfirmedAt" timestamp with time zone
);


--
-- Name: Operations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Operations" (
    "Key" text NOT NULL,
    "RequestHash" text NOT NULL,
    "ResponseJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Products; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Products" (
    "Id" uuid NOT NULL,
    "Sku" text NOT NULL,
    "Name" text NOT NULL,
    "Category" integer NOT NULL,
    "Active" boolean NOT NULL,
    "PriceRub" numeric(18,2) NOT NULL,
    "StockQuantity" integer NOT NULL,
    "SpecsJson" text NOT NULL,
    "SourceUrl" text NOT NULL,
    "SourceNotes" text NOT NULL,
    "PowerBasis" text NOT NULL,
    "PromotionPercent" numeric(18,2) NOT NULL,
    "PromotionFrom" timestamp with time zone,
    "PromotionTo" timestamp with time zone,
    "Version" integer NOT NULL
);


--
-- Name: Quotes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Quotes" (
    "Id" uuid NOT NULL,
    "Owner" text NOT NULL,
    "CartId" uuid,
    "ItemsJson" text NOT NULL,
    "QuoteJson" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL
);


--
-- Name: Sales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Sales" (
    "Id" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Category" integer NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPriceRub" numeric(18,2) NOT NULL,
    "DiscountPercent" numeric(18,2) NOT NULL,
    "UnitPriceAfterDiscountRub" numeric(18,2) NOT NULL,
    "TotalRub" numeric(18,2) NOT NULL,
    "ConfirmedAt" timestamp with time zone NOT NULL
);


--
-- Name: Sessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Sessions" (
    "TokenHash" text NOT NULL,
    "UserId" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL
);


--
-- Name: Users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Users" (
    "Id" uuid NOT NULL,
    "Login" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Role" text NOT NULL,
    "CustomerType" integer NOT NULL
);


--
-- Name: Carts PK_Carts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Carts"
    ADD CONSTRAINT "PK_Carts" PRIMARY KEY ("Id");


--
-- Name: Operations PK_Operations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Operations"
    ADD CONSTRAINT "PK_Operations" PRIMARY KEY ("Key");


--
-- Name: Products PK_Products; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Products"
    ADD CONSTRAINT "PK_Products" PRIMARY KEY ("Id");


--
-- Name: Quotes PK_Quotes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Quotes"
    ADD CONSTRAINT "PK_Quotes" PRIMARY KEY ("Id");


--
-- Name: Sales PK_Sales; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sales"
    ADD CONSTRAINT "PK_Sales" PRIMARY KEY ("Id");


--
-- Name: Sessions PK_Sessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "PK_Sessions" PRIMARY KEY ("TokenHash");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: IX_Carts_Status_ExpiresAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Carts_Status_ExpiresAt" ON public."Carts" USING btree ("Status", "ExpiresAt");


--
-- Name: IX_Products_Sku; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Products_Sku" ON public."Products" USING btree ("Sku");


--
-- Name: IX_Sales_ConfirmedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sales_ConfirmedAt" ON public."Sales" USING btree ("ConfirmedAt");


--
-- Name: IX_Sales_OrderId_ProductId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Sales_OrderId_ProductId" ON public."Sales" USING btree ("OrderId", "ProductId");


--
-- Name: IX_Sales_ProductId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sales_ProductId" ON public."Sales" USING btree ("ProductId");


--
-- Name: IX_Sessions_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sessions_UserId" ON public."Sessions" USING btree ("UserId");


--
-- Name: IX_Users_Login; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Users_Login" ON public."Users" USING btree ("Login");


--
-- Name: Sales FK_Sales_Products_ProductId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sales"
    ADD CONSTRAINT "FK_Sales_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES public."Products"("Id") ON DELETE RESTRICT;


--
-- Name: Sessions FK_Sessions_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "FK_Sessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict G1LfnD9M1r9VRofb1BIbalf7V3Pakogg6HXD1sRmZXH5ec1h135A7O3viGZrqn1
